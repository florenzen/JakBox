// Copyright (c) 2020, Florian Lorenzen
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//
// 3. Neither the name of the copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

module AudioRepository

open System
open Utils
open Fable.Core
open Fable.Core.JsInterop
open Fable.ReactNative.AndroidAudioStore
open Fable.Import.ReactNative.SqLiteStorage
open Fable.ReactNative.SqLiteStorage
open Fable.ReactNative
open Fable.ReactNativeFileSystem

type AudioRepo =
    { Database: ISqLiteDatabase
      RootDirectoryPaths: seq<string>
      DbName: string }

type private Track =
    { Id: int32
      Name: string
      AlbumId: int32
      TrackNumber: int32
      Duration: TimeSpan
      LastModified: DateTime
      Path: string }

let private initDirectoryTable (db: ISqLiteDatabase) =
    db.ExecuteSql "DROP TABLE IF EXISTS Directory"
    |> Promise.bind (fun _ ->
        db.ExecuteSql "CREATE TABLE IF NOT EXISTS Directory (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    DirectoryId INTEGER)"
        |> Promise.map (fun _ -> debug "initalized Directory table"))

let private initArtistTable (db: ISqLiteDatabase) =
    db.ExecuteSql "DROP TABLE IF EXISTS Artist"
    |> Promise.bind (fun _ ->
        db.ExecuteSql "CREATE TABLE IF NOT EXISTS Artist (
    Id INTEGER PRIMARY KEY,
    Name TEXT)"
        |> Promise.map (fun _ -> debug "initalized Artist table"))

let private initAlbumTable (db: ISqLiteDatabase) =
    db.ExecuteSql "DROP TABLE IF EXISTS Album"
    |> Promise.map (fun _ ->
        db.ExecuteSql "CREATE TABLE IF NOT EXISTS Album (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    NumTrack INTEGER,
    ArtistId INTEGER,
    Cover BLOB)"
        |> Promise.map (fun _ -> debug "initalized Album table"))

let private initTrackTable (db: ISqLiteDatabase) =
    db.ExecuteSql "DROP TABLE IF EXISTS Track"
    |> Promise.bind (fun _ ->
        db.ExecuteSql "CREATE TABLE IF NOT EXISTS Track (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    AlbumId INTEGER,
    TrackNumber INTEGER,
    Duration INTEGER,
    Filename TEXT,
    DirectoryId INTEGER,
    LastModified INTEGER)"
        |> Promise.map (fun _ -> debug "initalized Track table"))

let private generateSelectForFilePath (path: string): string * string [] =
    // path "/a/b/d/f2.mp3"
    // SELECT t.Id, t.Name FROM Track t
    // WHERE t.Name = 'f2.mp3'
    // AND (SELECT d2.Name FROM Directory d2
    //      WHERE d2.Id = t.DirectoryId
    //      AND (SELECT d1.Name FROM Directory d1
    //           WHERE d1.Id = d2.DirectoryId
    //           AND (SELECT d0.Name FROM Directory d0
    //                WHERE d0.Id = d1.DirectoryId
    //                AND d0.DirectoryId IS NULL) = 'a') = 'b') = 'd';
    let rec conditionsFromDirectories (directories: string list) (innerCondition: string) (index: int32) =
        match directories with
        | [] -> failwith "cannot be called on an empty list"
        | [ directory ] ->
            sprintf
                "(SELECT d%i.Name FROM Directory d%i WHERE d%i.Id = t.DirectoryId AND %s) = ?"
                index
                index
                index
                innerCondition
        | directory :: directories ->
            let condition =
                sprintf
                    "(SELECT d%i.Name FROM Directory d%i WHERE d%i.Id = d%i.DirectoryId AND %s) = ?"
                    index
                    index
                    index
                    (index + 1)
                    innerCondition

            conditionsFromDirectories directories condition (index + 1)

    let (directories, filename) = Path.splitFilename path
    let directoriesList = Seq.toList directories

    let condFromDirectories =
        match directoriesList with
        | [] -> ""
        | _ :: _ ->
            "AND "
            + conditionsFromDirectories directoriesList "d0.DirectoryId IS NULL" 0

    ("SELECT t.Id, t.Name, AlbumId, TrackNumber, Duration, LastModified FROM Track t "
     + "WHERE t.Name = ? "
     + condFromDirectories
     + " LIMIT 1",
     Array.ofList (filename :: directoriesList))

let private trackFromRow (row: obj) (path: string) =
    let duration = TimeSpan.FromTicks(int64 row?Duration)

    let lastModified =
        DateTime(int64 row?LastModified, DateTimeKind.Utc)

    { Id = int32 row?Id
      Name = row?Name
      AlbumId = int32 row?AlbumId
      TrackNumber = int32 row?TrackNumber
      Duration = duration
      LastModified = lastModified
      Path = path }

let private findTrackByPath (db: ISqLiteDatabase) (path: string): JS.Promise<Track option> =
    let (select, arguments) = generateSelectForFilePath path

    db.ExecuteSql(select, arguments |> Array.map (fun arg -> arg :> obj))
    |> Promise.map (fun result ->
        let rows = result.Rows
        if rows.Length = 0 then
            None
        else
            let row = rows.Item 0
            Some(trackFromRow row path))

let private findTracksByIds (db: ISqLiteDatabase) (ids: int32 []) (invert: bool): JS.Promise<(int32 * Track option) list> =
    let idsAsSqlList =
        ids
        |> Array.map (fun id -> id.ToString())
        |> String.concat ","

    let select =
        sprintf "SELECT
    t.Id,
    t.Name,
    t.AlbumId,
    t.TrackNumber,
    t.Duration,
    t.LastModified,
    (
        WITH Directories(Id, Pos) AS (
            SELECT DirectoryId, 0 FROM Track WHERE Id = t.Id
            UNION ALL
            SELECT
                (SELECT DirectoryId FROM Directory d WHERE d.Id = ds.Id),
                ds.Pos+1
            FROM Directories ds
            WHERE ds.Id IS NOT NULL)
        SELECT
            '/' ||
            GROUP_CONCAT(Name, '/') ||
            '/' ||
            (SELECT Name From Track WHERE Id = t.Id) AS Path
        FROM (
            SELECT (SELECT d.Name FROM Directory d WHERE d.Id = ds.Id) AS Name
            FROM Directories ds
            WHERE Name IS NOT NULL
            ORDER BY ds.Pos DESC)) AS Path
FROM Track t
WHERE t.Id %sIN (%s)" (if invert then "NOT " else "") idsAsSqlList

    db.ExecuteSql select
    |> Promise.map (fun result ->
        let rows = result.Rows

        let idsAndTracks =
            [ for idx in 0 .. rows.Length - 1 ->
                let track =
                    trackFromRow (rows.Item idx) (rows.Item idx)?Path

                (track.Id, Some track) ]

        let trackIds = idsAndTracks |> List.map fst

        let idsNotFound = List.ofArray ids |> List.except trackIds

        List.append idsAndTracks (List.map (fun id -> (id, None)) idsNotFound))

let private initTables (db: ISqLiteDatabase) =
    initDirectoryTable db
    |> Promise.bind (fun _ ->
        initArtistTable db
        |> Promise.bind (fun _ ->
            initAlbumTable db
            |> Promise.bind (fun _ -> initTrackTable db)))

let private findAllAudioFilesWithModificationTime (rootDirectoryPaths: seq<string>) =
    getAll (GetAllOptions())
    |> Promise.bind (fun tracks ->
        tracks
        |> Array.map (fun track -> track.Path)
        |> Array.filter (fun path -> Seq.exists (Path.isSubPath path) rootDirectoryPaths)
        |> Array.map (fun path ->
            stat path
            |> Promise.map (fun statResult -> (path, statResult)))
        |> Promise.Parallel
        |> Promise.map (fun results ->
            results
            |> Array.map (fun (path, statResult) -> (path, statResult.Mtime))
            |> List.ofArray))

let openRepo (dbName: string) (rootDirectoryPaths: seq<string>) =
    assert (Seq.forall Path.isAbsolute rootDirectoryPaths)
#if DEBUG
    setDebugMode true
#endif
    openDatabase dbName
    |> Promise.bind (fun db ->
        debug "opened repo database %s" dbName
        initTables db
        |> Promise.map (fun _ ->
            { Database = db
              DbName = dbName
              RootDirectoryPaths = rootDirectoryPaths }))

let closeRepo (repo: AudioRepo) =
    repo.Database.Close()
    |> Promise.map (fun () -> debug "closed repo database %s" repo.DbName)

let private statList (paths: string list) = paths |> List.map stat |> Promise.all

type private LookupResult =
    { Path: string
      ModificationTime: DateTime
      MaybeTrack: Track option }

type private Changes =
    { Added: seq<LookupResult>
      Changed: seq<LookupResult>
      Removed: seq<Track> }

let private changesToText (allChanges: Changes): string =
    let addedText =
        "ADDED\n"
        + (allChanges.Added
           |> Seq.map (fun lookupResult -> lookupResult.Path)
           |> String.concat "\n")

    let changedText =
        "CHANGED"
        + (allChanges.Changed
           |> Seq.map (fun lookupResult -> lookupResult.MaybeTrack.Value.Path)
           |> String.concat "\n")

    let removedText =
        "REMOVED"
        + (allChanges.Removed
           |> Seq.map (fun track -> track.Path)
           |> String.concat "\n")

    String.concat "\n" [ addedText; changedText; removedText ]

let private (+) (left: Changes) (right: Changes) =
    { Added = Seq.append left.Added right.Added
      Changed = Seq.append left.Changed right.Changed
      Removed = Seq.append left.Removed right.Removed }

let private lookupTracksByPaths (db: ISqLiteDatabase)
                                (pathsAndModificationTimes: (string * DateTime) list)
                                : JS.Promise<LookupResult []> =
    let lookupPromises =
        pathsAndModificationTimes
        |> List.map (fun (path, modTime) ->
            findTrackByPath db path
            |> Promise.map (fun maybeTrack ->
                { Path = path
                  ModificationTime = modTime
                  MaybeTrack = maybeTrack }))

    Promise.Parallel(Array.ofList lookupPromises)

let private addedAndChangedFromLookupResults (lookupResults: seq<LookupResult>) =
    lookupResults
    |> List.ofSeq
    |> List.partition (fun lookupResult -> lookupResult.MaybeTrack.IsNone)
    |> fun (added, possiblyChanged) ->
        { Added = added
          Changed =
              possiblyChanged
              |> List.filter (fun lookupResult ->
                  lookupResult.ModificationTime > lookupResult.MaybeTrack.Value.LastModified)
          Removed = List.empty }

let private removedFromLookupResults (db: ISqLiteDatabase) (lookupResults: seq<LookupResult>) =
    let ids =
        lookupResults
        |> Seq.filter (fun lookupResult -> lookupResult.MaybeTrack.IsSome)
        |> Seq.map (fun lookupResult -> lookupResult.MaybeTrack.Value.Id)
        |> Array.ofSeq

    findTracksByIds db ids true
    |> Promise.map (fun results ->
        let removed =
            results
            |> List.filter (snd >> Option.isSome)
            |> List.map (snd >> Option.get)

        { Added = List.empty
          Changed = List.empty
          Removed = removed })

// let private findAllChanges (repo: AudioRepo): JS.Promise<Changes> =
//     findAllAudioFilesWithModificationTime repo.RootDirectoryPaths
//     |> Promise.bind (fun pathsAndModTimes ->
//         repo.Database.Transaction(fun tx ->
//             lookupTracksByPaths tx pathsAndModTimes
//             |> Promise.map (fun lookupResults ->
//                 let changesAddedChanged =
//                     addedAndChangedFromLookupResults lookupResults

//                 repo.Database.Transaction(fun tx ->
//                     removedFromLookupResults tx lookupResults
//                     |> Promise.map (fun changesRemoved ->
//                         let allChanges = changesAddedChanged + changesRemoved
//                         debug "%s" (changesToText allChanges))
//                     |> ignore)



//                 )
//             |> ignore)
//         |> Promise.map (fun _ -> repo))


let private findAllChanges (repo: AudioRepo): JS.Promise<Changes> =
    findAllAudioFilesWithModificationTime repo.RootDirectoryPaths
    |> Promise.bind (fun pathsAndModTimes ->
        lookupTracksByPaths repo.Database pathsAndModTimes
        |> Promise.bind (fun lookupResults ->
            let changesAddedChanged =
                addedAndChangedFromLookupResults lookupResults

            removedFromLookupResults repo.Database lookupResults
            |> Promise.map (fun changesRemoved ->
                let allChanges = changesAddedChanged + changesRemoved
                debug "%s" (changesToText allChanges)
                allChanges)))

let findArtistIdByName (db: ISqLiteDatabase) (artist: string): JS.Promise<int32 option> =
    db.ExecuteSql("SELECT Id FROM Artist WHERE Name = ?", [| artist |])
    |> Promise.bind (fun result ->
        let rows = result.Rows
        if rows.Length > 0 then
            let id = result.Rows.Item(0) :?> int32
            Promise.lift (Some id)
        else
            Promise.lift None)

let private addArtistToDb (db: ISqLiteDatabase) (artist: string): JS.Promise<unit> =
    db.ExecuteSql("INSERT OR IGNORE INTO Artist (Name) SELECT ? WHERE NOT EXISTS (SELECT * FROM Artist WHERE Name = ?)", [|artist;artist|])
    |> Promise.map ignore

    // findArtistIdByName db artist
    // |> Promise.bind (fun maybeArtistId ->
    //     match maybeArtistId with
    //     | None ->
    //         db.ExecuteSql("INSERT INTO Artist (Name) VALUES (?)", [| artist |])
    //         |> Promise.bind (fun _ ->
    //             findArtistIdByName db artist
    //             |> Promise.map (fun someArtistId ->
    //                 let id = Option.get someArtistId
    //                 debug "artist %s added with id %i" artist id
    //                 id))
    //     | Some id ->
    //         debug "artist %s already in db with id %i" artist id
    //         Promise.lift id)

let rec private sequentialize (promises: JS.Promise<'T> list): JS.Promise<'T list> =
    match promises with
    | [] -> Promise.lift []
    | promise :: promises1 ->
        promise
        |> Promise.bind (fun result ->
            sequentialize promises1
            |> Promise.bind (fun results -> Promise.lift (result :: results)))

let private writeAddedToDb (db: ISqLiteDatabase) (added: seq<LookupResult>) =
    added
    |> Seq.toList
    |> List.map (fun lookupResult ->
        debug "reading tags of %s" lookupResult.Path
        JsMediaTags.readTags
            lookupResult.Path
            [ JsMediaTags.Title
              JsMediaTags.Artist
              JsMediaTags.Album
              JsMediaTags.Track ]

        |> Promise.bind (fun id3 ->
            debug "adding artist %s to db" id3.Tags.Artist
            addArtistToDb db id3.Tags.Artist))
    |> sequentialize


let updateRepo (repo: AudioRepo): JS.Promise<AudioRepo> =
    findAllChanges repo
    |> Promise.bind (fun changes ->
        writeAddedToDb repo.Database changes.Added
        |> Promise.bind (fun _ ->
            repo.Database.ExecuteSql("SELECT * FROM Artist")
            |> Promise.map (fun result ->
                debug "COUNT %i" result.Rows.Length
                repo)))




// lookupResults

// |> Array.iter (fun lookupResult ->
//     let trackString =
//         match lookupResult.MaybeTrack with
//         | None -> "None"
//         | Some t -> sprintf "{Name = %s, ModTime = %s}" t.Name (t.LastModified.ToString())

//     debug
//         "Path: %s, modTime: %s, %s"
//         lookupResult.Path
//         (lookupResult.ModificationTime.ToString())
//         trackString)





let updateRepo1 (repo: AudioRepo) =
    findAllAudioFilesWithModificationTime repo.RootDirectoryPaths
    |> Promise.bind (fun pathsAndModTimes ->
        let (path, modTime) = List.head pathsAndModTimes
        JsMediaTags.read path
        |> Promise.bind (fun id3 ->
            debug "ID3: %s %s %s" id3.Tags.Artist id3.Tags.Album id3.Tags.Title
            stat path
            |> Promise.bind (fun stat ->
                debug
                    "path: %s, size: %i, mtime: %s, ctime: %s"
                    path
                    stat.Size
                    (stat.Mtime.ToString())
                    (stat.Ctime.ToString())
                repo.Database.Transaction(fun tx ->
                    tx.ExecuteSql("SELECT * FROM Track LIMIT 1")
                    |> Promise.map (fun (_, result) ->
                        let rows = result.Rows
                        debug "row count %i" rows.Length
                        let row = rows.Item 0
                        debug "row 0 %O" row?Name)
                    |> ignore)
                |> Promise.map (fun _ -> repo))))
