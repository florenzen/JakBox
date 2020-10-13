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

type Track =
    { Id: int32
      Name: string
      AlbumId: int32
      TrackNumber: int32
      Duration: TimeSpan
      LastModified: DateTime
      Path: string }

let private initDirectoryTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "DROP TABLE IF EXISTS Directory"
    |> ignore
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Directory (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    DirectoryId INTEGER)"
    |> ignore
    debug "initalized Directory table"

let private initArtistTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "DROP TABLE IF EXISTS Artist"
    |> ignore
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Artist (
    Id INTEGER PRIMARY KEY,
    Name TEXT)"
    |> ignore
    debug "initalized Artist table"

let private initAlbumTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "DROP TABLE IF EXISTS Album"
    |> ignore
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Album (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    NumTrack INTEGER,
    ArtistId INTEGER,
    Cover BLOB)"
    |> ignore
    debug "initalized Album table"

let private initTrackTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "DROP TABLE IF EXISTS Track"
    |> ignore
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Track (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    AlbumnId INTEGER,
    TrackNumber INTEGER,
    Duration INTEGER,
    Filename TEXT,
    DirectoryId INTEGER,
    LastModified INTEGER)"
    |> ignore
    tx.ExecuteSql "INSERT INTO Track (Name, TrackNumber, Duration) VALUES ('Foo', 1, '10000')"
    |> ignore
    tx.ExecuteSql "INSERT INTO Track (Name, TrackNumber, Duration) VALUES ('Bar', 2, '20000')"
    |> ignore
    debug "initalized Track table"

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

let private findTrackByPath (tx: ISqLiteTransaction) (path: string): JS.Promise<Track option> =
    let (select, arguments) = generateSelectForFilePath path

    tx.ExecuteSql(select, arguments |> Array.map (fun arg -> arg :> obj))
    |> Promise.map (fun (_, result) ->
        let rows = result.Rows
        if rows.Length = 0 then
            None
        else
            let row = rows.Item 0
            let duration = TimeSpan.FromTicks(int64 row?Duration)

            let lastModified =
                DateTime(int64 row?LastModified, DateTimeKind.Utc)

            Some
                { Id = int32 row?Id
                  Name = row?Name
                  AlbumId = int32 row?AlbumId
                  TrackNumber = int32 row?TrackNumber
                  Duration = duration
                  LastModified = lastModified
                  Path = path })





// dir1 dir2 dir3 dir4 fname
// SELECT t.Id, t.Name FROM Track t
// WHERE t.Filename = filename
// AND (SELECT d4.Name FROM Directories d4
//      WHERE d4.Id = t.DirectoryId
//      AND (SELECT d3.Name FROM Directories d3
//           WHERE d3.Id = d4.Id
//           AND (SELECT d2.Name FROM Directoris d2) =
//                ) = dir3) = dir4



let private initTables (db: ISqLiteDatabase) =
    db.Transaction(fun tx ->
        initDirectoryTable tx
        initArtistTable tx
        initAlbumTable tx
        initTrackTable tx)

let findAllAudioFilesWithModificationTime (rootDirectoryPaths: seq<string>) =
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

let statList (paths: string list) = paths |> List.map stat |> Promise.all

let updateRepo (repo: AudioRepo) =
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
