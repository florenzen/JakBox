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

type private TaggedTrack =
    { Name: string
      Album: string
      Artist: string
      TrackNumber: int32
      Duration: TimeSpan
      LastModified: DateTime
      Path: string }

let private initDirectoryTable (db: ISqLiteDatabase) =
    // db.ExecuteSql "DROP TABLE IF EXISTS Directory"
    // |> Promise.bind (fun _ ->
    db.ExecuteSql
        "CREATE TABLE IF NOT EXISTS Directory (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    DirectoryId INTEGER)"
    |> Promise.map (fun _ -> debug "initalized Directory table")

let private initArtistTable (db: ISqLiteDatabase) =
    // db.ExecuteSql "DROP TABLE IF EXISTS Artist"
    // |> Promise.bind (fun _ ->
    db.ExecuteSql
        "CREATE TABLE IF NOT EXISTS Artist (
    Id INTEGER PRIMARY KEY,
    Name TEXT)"
    |> Promise.map (fun _ -> debug "initalized Artist table")

let private initAlbumTable (db: ISqLiteDatabase) =
    // db.ExecuteSql "DROP TABLE IF EXISTS Album"
    // |> Promise.map (fun _ ->
    db.ExecuteSql
        "CREATE TABLE IF NOT EXISTS Album (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    NumTrack INTEGER,
    ArtistId INTEGER,
    Cover BLOB)"
    |> Promise.map (fun _ -> debug "initalized Album table")

let private initTrackTable (db: ISqLiteDatabase) =
    // db.ExecuteSql "DROP TABLE IF EXISTS Track"
    // |> Promise.bind (fun _ ->
    db.ExecuteSql
        "CREATE TABLE IF NOT EXISTS Track (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    AlbumId INTEGER,
    TrackNumber INTEGER,
    Duration INTEGER,
    Filename TEXT,
    DirectoryId INTEGER,
    LastModified INTEGER)"
    |> Promise.map (fun _ -> debug "initalized Track table")

let private generateSelectForFilePath (path: string) : string * string [] =
    // path "/a/b/d/f2.mp3"
    // SELECT t.Id, t.Name FROM Track t
    // WHERE t.Filename = 'f2.mp3'
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
        | [ _ ] ->
            sprintf
                "(SELECT d%i.Name FROM Directory d%i WHERE d%i.Id = t.DirectoryId AND %s) = ?"
                index
                index
                index
                innerCondition
        | _ :: directories ->
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
     + "WHERE t.Filename = ? "
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

let private findTrackByPath (db: ISqLiteDatabase) (path: string) : JS.Promise<Track option> =
    let (select, arguments) = generateSelectForFilePath path
    debug "select for path %s: %s (arguments = %s)" path select (String.concat "," arguments)

    db.ExecuteSql(select, arguments |> Array.map (fun arg -> arg :> obj))
    |> Promise.map
        (fun result ->
            let rows = result.Rows
            debug "rows length %i" rows.Length

            if rows.Length = 0 then
                None
            else
                let row = rows.Item 0
                Some(trackFromRow row path))

let private findTracksByIds
    (db: ISqLiteDatabase)
    (ids: int32 [])
    (invert: bool)
    : JS.Promise<(int32 * Track option) list> =
    let idsAsSqlList =
        ids
        |> Array.map (fun id -> id.ToString())
        |> String.concat ","

    let select =
        sprintf
            "SELECT
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
WHERE t.Id %sIN (%s)"
            (if invert then "NOT " else "")
            idsAsSqlList

    db.ExecuteSql select
    |> Promise.map
        (fun result ->
            let rows = result.Rows

            let idsAndTracks =
                [ for idx in 0 .. rows.Length - 1 ->
                      let track =
                          trackFromRow (rows.Item idx) (rows.Item idx)?Path

                      (track.Id, Some track) ]

            let trackIds = idsAndTracks |> List.map fst

            let idsNotFound = List.ofArray ids |> List.except trackIds

            List.append idsAndTracks (List.map (fun id -> (id, None)) idsNotFound))

let private findAlbumAndArtistByAlbumId (db: ISqLiteDatabase) (albumId: int32) =
    db.ExecuteSql(
        "SELECT alb.Name AS AlbumName, art.Name AS ArtistName
FROM Album alb
JOIN Artist art
ON alb.ArtistId = art.Id
WHERE alb.Id = ?
LIMIT 1",
        [| albumId |]
    )
    |> Promise.map
        (fun result ->
            if result.Rows.Length = 0 then
                None
            else
                let albumName =
                    result.Rows.Item(0)?AlbumName :> obj :?> string

                let artistName =
                    result.Rows.Item(0)?ArtistName :> obj :?> string

                Some(albumName, artistName))

let private initTables (db: ISqLiteDatabase) =
    promise {
        let! _ = initDirectoryTable db
        let! _ = initArtistTable db
        let! _ = initAlbumTable db
        initTrackTable db |> ignore
    }

let private findAllAudioFilesWithModificationTime (rootDirectoryPaths: seq<string>) =
    getAll (GetAllOptions())
    |> Promise.bind
        (fun tracks ->
            tracks
            |> Array.map (fun track -> track.Path)
            |> Array.filter (fun path -> Seq.exists (Path.isSubPath path) rootDirectoryPaths)
            |> Array.map
                (fun path ->
                    stat path
                    |> Promise.map (fun statResult -> (path, statResult)))
            |> Promise.all
            |> Promise.map
                (fun results ->
                    results
                    |> Array.map (fun (path, statResult) -> (path, statResult.Mtime))
                    |> List.ofArray))

let openRepo (dbName: string) (rootDirectoryPaths: seq<string>) =
    assert (Seq.forall Path.isAbsolute rootDirectoryPaths)
#if DEBUG
    setDebugMode true
#endif
    openDatabase dbName
    |> Promise.bind
        (fun db ->
            debug "opened repo database %s" dbName

            initTables db
            |> Promise.map
                (fun _ ->
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

let private changesToText (allChanges: Changes) : string =
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

let private (++) (left: Changes) (right: Changes) =
    { Added = Seq.append left.Added right.Added
      Changed = Seq.append left.Changed right.Changed
      Removed = Seq.append left.Removed right.Removed }

let rec private lookupTracksByPaths
    (db: ISqLiteDatabase)
    (pathsAndModificationTimes: (string * DateTime) list)
    : JS.Promise<LookupResult list> =

    match pathsAndModificationTimes with
    | [] -> Promise.lift []
    | (path, modificationTime) :: rest ->
        findTrackByPath db path
        |> Promise.bind
            (fun maybeTrack ->
                lookupTracksByPaths db rest
                |> Promise.bind
                    (fun lookupResults ->
                        let lookupResult =
                            { Path = path
                              ModificationTime = modificationTime
                              MaybeTrack = maybeTrack }

                        Promise.lift (lookupResult :: lookupResults)))

let private addedAndChangedFromLookupResults (lookupResults: seq<LookupResult>) =
    lookupResults
    |> List.ofSeq
    |> List.partition (fun lookupResult -> lookupResult.MaybeTrack.IsNone)
    |> fun (added, possiblyChanged) ->
        { Added = added
          Changed =
              possiblyChanged
              |> List.filter
                  (fun lookupResult -> lookupResult.ModificationTime > lookupResult.MaybeTrack.Value.LastModified)
          Removed = List.empty }

let private removedFromLookupResults (db: ISqLiteDatabase) (lookupResults: seq<LookupResult>) =
    let ids =
        lookupResults
        |> Seq.filter (fun lookupResult -> lookupResult.MaybeTrack.IsSome)
        |> Seq.map (fun lookupResult -> lookupResult.MaybeTrack.Value.Id)
        |> Array.ofSeq

    findTracksByIds db ids true
    |> Promise.map
        (fun results ->
            let removed =
                results
                |> List.filter (snd >> Option.isSome)
                |> List.map (snd >> Option.get)

            { Added = List.empty
              Changed = List.empty
              Removed = removed })


let private findAllChanges (repo: AudioRepo) : JS.Promise<Changes> =
    findAllAudioFilesWithModificationTime repo.RootDirectoryPaths
    |> Promise.bind
        (fun pathsAndModTimes ->
            lookupTracksByPaths repo.Database pathsAndModTimes
            |> Promise.bind
                (fun lookupResults ->
                    lookupResults
                    |> List.iter (fun result -> debug "lookup result %O" result)

                    let changesAddedChanged =
                        addedAndChangedFromLookupResults lookupResults

                    removedFromLookupResults repo.Database lookupResults
                    |> Promise.map
                        (fun changesRemoved ->
                            let allChanges = changesAddedChanged ++ changesRemoved
                            debug "%s" (changesToText allChanges)
                            allChanges)))

let private insertDirectories (db: ISqLiteDatabase) (path: string) =
    let (directories, _) = Path.splitFilename path

    let rec insertDirectoriesWithParent (directories: string list) (directoryId: int32 option) =
        match directories with
        | [] -> Promise.lift directoryId
        | root :: subs ->
            match directoryId with
            | Some (id) ->
                db.ExecuteSql(
                    "INSERT OR IGNORE INTO Directory (Name, DirectoryId) SELECT ?, ? WHERE NOT EXISTS (SELECT * FROM Directory WHERE Name = ? AND DirectoryId = ?)",
                    [| root; id; root; id |]
                )

            | None ->
                db.ExecuteSql(
                    "INSERT OR IGNORE INTO Directory (Name) SELECT ? WHERE NOT EXISTS (SELECT * FROM Directory WHERE Name = ? AND DirectoryId IS NULL)",
                    [| root; root |]
                )
            |> Promise.bind
                (fun _ ->
                    debug "inserted DIR %s" root

                    match directoryId with
                    | Some (id) ->
                        db.ExecuteSql("SELECT Id FROM Directory WHERE Name = ? AND DirectoryId = ?", [| root; id |])
                        |> Promise.bind
                            (fun result ->
                                let parentId = result.Rows.Item(0)?Id :> obj :?> int32
                                insertDirectoriesWithParent subs (Some(parentId)))
                    | None ->
                        db.ExecuteSql("SELECT Id FROM Directory WHERE Name = ? AND DirectoryId IS NULL", [| root |])
                        |> Promise.bind
                            (fun result ->
                                let parentId = result.Rows.Item(0)?Id :> obj :?> int32
                                insertDirectoriesWithParent subs (Some(parentId))))

    insertDirectoriesWithParent (Seq.toList directories) None

let rec private insertTaggedTracks (db: ISqLiteDatabase) (albumId: int32) (taggedTracks: TaggedTrack list) =
    let insertSingleTaggedTrack (taggedTrack: TaggedTrack) =
        debug "going to insert %s" taggedTrack.Name

        insertDirectories db taggedTrack.Path
        |> Promise.bind
            (fun maybeDirectoryId ->
                db.ExecuteSql(
                    "INSERT INTO Track (Name, AlbumId, TrackNumber, Duration, Filename, LastModified, DirectoryId) VALUES (?, ?, ?, ?, ?, ?, ?)",
                    [| taggedTrack.Name
                       albumId
                       taggedTrack.TrackNumber
                       0 // TODO
                       Path.filename taggedTrack.Path
                       0 // TODO
                       maybeDirectoryId |]
                )
                |> Promise.bind
                    (fun _ ->
                        debug "inserted %s" taggedTrack.Name
                        Promise.lift ()))

    match taggedTracks with
    | [] -> Promise.lift ()
    | taggedTrack :: taggedTracks ->
        insertSingleTaggedTrack taggedTrack
        |> Promise.bind (fun _ -> insertTaggedTracks db albumId taggedTracks)

let private insertSingleAlbum (db: ISqLiteDatabase) (artistId: int32) (album: string) =
    db.ExecuteSql(
        "INSERT OR IGNORE INTO Album (Name, ArtistId) SELECT ?, ? WHERE NOT EXISTS (SELECT * FROM Album WHERE Name = ? AND ArtistId = ?); ",
        [| album; artistId; album; artistId |]
    )
    |> Promise.bind
        (fun _ ->
            db.ExecuteSql("SELECT Id FROM Album WHERE Name = ?", [| album |])
            |> Promise.bind
                (fun result ->
                    let albumId = int32 (result.Rows.Item(0))?Id
                    debug "inserted %s as %i" album albumId
                    Promise.lift albumId))

let rec private addGroupedByAlbum (db: ISqLiteDatabase) (artistId: int32) (byAlbum: (string * TaggedTrack list) list) =
    let addSingleAlbum (album: string) (taggedTracks: TaggedTrack list) =
        insertSingleAlbum db artistId album
        |> Promise.bind (fun albumId -> insertTaggedTracks db albumId taggedTracks)

    match byAlbum with
    | [] -> Promise.lift ()
    | (album, taggedTracks) :: byAlbums ->
        addSingleAlbum album taggedTracks
        |> Promise.bind (fun _ -> addGroupedByAlbum db artistId byAlbums)


let private insertSingleArtist (db: ISqLiteDatabase) (artist: string) =
    debug "insert artist %s" artist

    db.ExecuteSql(
        "INSERT OR IGNORE INTO Artist (Name) SELECT ? WHERE NOT EXISTS (SELECT * FROM Artist WHERE Name = ?); ",
        [| artist; artist |]
    )
    |> Promise.bind
        (fun _ ->
            db.ExecuteSql("SELECT Id FROM Artist WHERE Name = ?", [| artist |])
            |> Promise.bind
                (fun result ->
                    let artistId = int32 (result.Rows.Item(0))?Id
                    debug "inserted %s as %i" artist artistId
                    Promise.lift artistId))


let rec private addGroupedByArtist (db: ISqLiteDatabase) (byArtist: (string * (string * TaggedTrack list) list) list) =
    let addSingleArtist (artist: string) (byAlbum: (string * TaggedTrack list) list) =
        insertSingleArtist db artist
        |> Promise.bind (fun artistId -> addGroupedByAlbum db artistId byAlbum)

    match byArtist with
    | [] -> Promise.lift ()
    | (artist, byAlbum) :: byArtists ->
        addSingleArtist artist byAlbum
        |> Promise.bind (fun _ -> addGroupedByArtist db byArtists)


let private addTaggedTracks (db: ISqLiteDatabase) (taggedTracks: TaggedTrack list) =
    taggedTracks
    |> List.groupBy (fun taggedTrack -> taggedTrack.Artist)
    |> List.map
        (fun (artist, taggedTracks) ->
            (artist,
             taggedTracks
             |> List.groupBy (fun taggedTrack -> taggedTrack.Album)))
    |> addGroupedByArtist db


let private readTagsFromLookupResults (results: seq<LookupResult>) =
    results
    |> Seq.map
        (fun lookupResult ->
            debug "reading tags of %s" lookupResult.Path

            JsMediaTags.readTags
                lookupResult.Path
                [ JsMediaTags.Title
                  JsMediaTags.Artist
                  JsMediaTags.Album
                  JsMediaTags.Track ]

            |> Promise.map
                (fun id3 ->
                    { Name = id3.Tags.Title
                      Artist = id3.Tags.Artist
                      Album = id3.Tags.Album
                      TrackNumber = 0 // TODO
                      Duration = TimeSpan.Zero // TODO
                      LastModified = lookupResult.ModificationTime
                      Path = lookupResult.Path }))
    |> Promise.all
    |> Promise.map List.ofArray


let private updateAlbumAndArtistOfTrack
    (db: ISqLiteDatabase)
    (album: string)
    (artist: string)
    (track: Track)
    (taggedTrack: TaggedTrack)
    =
    if album <> taggedTrack.Album
       || artist <> taggedTrack.Artist then
        db.ExecuteSql(
            "SELECT Id
FROM Album alb
JOIN Artist art
ON alb.ArtistId = art.Id
WHERE alb.Name = ?
AND art.Name = ?
LIMIT 1",
            [| taggedTrack.Album
               taggedTrack.Artist |]
        )
        |> Promise.bind
            (fun result ->
                if result.Rows.Length = 0 then
                    promise {
                        let! artistId = insertSingleArtist db taggedTrack.Artist
                        let! albumId = insertSingleAlbum db artistId taggedTrack.Album

                        db.ExecuteSql("UPDATE Track SET Album = ? WHERE Id = ?", [| albumId; track.Id |])
                        |> ignore
                    }
                else
                    db.ExecuteSql("UPDATE Track SET AlbumId = ? WHERE Id = ?", [| result.Rows.Item(0)?Id; track.Id |])
                    |> Promise.map ignore)
    else
        Promise.lift ()


let private updateSimpleValuesOfTrack (db: ISqLiteDatabase) (taggedTrack: TaggedTrack) (track: Track) =
    db.ExecuteSql(
        "UPDATE Track
SET Name = ?,
    TrackNumber = ?,
    Duration = ?,
    LastModified = ?
WHERE Id = ?",
        [| taggedTrack.Name
           taggedTrack.TrackNumber
           int64 taggedTrack.Duration.Ticks
           int64 taggedTrack.LastModified.Ticks
           track.Id |]
    )
    |> Promise.map ignore


let rec private updateTaggedTracks (db: ISqLiteDatabase) (taggedTracks: TaggedTrack list) =
    let updateSingleTaggedTrack (taggedTrack: TaggedTrack) =
        findTrackByPath db taggedTrack.Path
        |> Promise.bind
            (fun maybeTrack ->
                match maybeTrack with
                | None ->
                    debug "ERROR: changed track with path %s not found in database" taggedTrack.Path
                    Promise.lift ()
                | Some track ->
                    findAlbumAndArtistByAlbumId db track.AlbumId
                    |> Promise.bind
                        (fun maybeAlbumandArtist ->
                            match maybeAlbumandArtist with
                            | None ->
                                debug "ERROR: could not find album and artist for album id %i" track.AlbumId
                                Promise.lift ()
                            | Some (album, artist) ->
                                updateAlbumAndArtistOfTrack db album artist track taggedTrack
                                |> Promise.bind (fun _ -> updateSimpleValuesOfTrack db taggedTrack track)))

    match taggedTracks with
    | [] -> Promise.lift ()
    | track :: tracks ->
        updateSingleTaggedTrack track
        |> Promise.bind (fun _ -> updateTaggedTracks db tracks)


let private writeAddedToDb (db: ISqLiteDatabase) (added: seq<LookupResult>) =
    added
    |> readTagsFromLookupResults
    |> Promise.bind (List.ofSeq >> addTaggedTracks db)


let private writeChangedToDb (db: ISqLiteDatabase) (changed: seq<LookupResult>) =
    changed
    |> readTagsFromLookupResults
    |> Promise.bind (updateTaggedTracks db)


let updateRepo (repo: AudioRepo) : JS.Promise<AudioRepo> =
    promise {
        let! changes = findAllChanges repo
        let! _ = writeAddedToDb repo.Database changes.Added
        let! _ = writeChangedToDb repo.Database changes.Changed
        // TODO remove deleted
        // TODO delete unref'd artists and albums and directories
        // TODO fix missing track numbers
        // TODO read album covers as first readable cover of files in track order of that album
        let! numberOfAlbumsResult = repo.Database.ExecuteSql("SELECT * FROM Album")
        debug "number of albums %i" numberOfAlbumsResult.Rows.Length
        let! numberOfTracksResult = repo.Database.ExecuteSql("SELECT * FROM Track")
        debug "number of tracks %i" numberOfTracksResult.Rows.Length
        return repo
    }


let updateRepo1 (repo: AudioRepo) =
    findAllAudioFilesWithModificationTime repo.RootDirectoryPaths
    |> Promise.bind
        (fun pathsAndModTimes ->
            let (path, modTime) = List.head pathsAndModTimes

            JsMediaTags.read path
            |> Promise.bind
                (fun id3 ->
                    debug "ID3: %s %s %s" id3.Tags.Artist id3.Tags.Album id3.Tags.Title

                    stat path
                    |> Promise.bind
                        (fun stat ->
                            debug
                                "path: %s, size: %i, mtime: %s, ctime: %s"
                                path
                                stat.Size
                                (stat.Mtime.ToString())
                                (stat.Ctime.ToString())

                            repo.Database.Transaction
                                (fun tx ->
                                    tx.ExecuteSql("SELECT * FROM Track LIMIT 1")
                                    |> Promise.map
                                        (fun (_, result) ->
                                            let rows = result.Rows
                                            debug "row count %i" rows.Length
                                            let row = rows.Item 0
                                            debug "row 0 %O" row?Name)
                                    |> ignore)
                            |> Promise.map (fun _ -> repo))))
