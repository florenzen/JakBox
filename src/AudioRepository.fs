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

open Utils
open Fable.ReactNative.AndroidAudioStore
open Fable.Import.ReactNative.SqLiteStorage
open Fable.ReactNative.SqLiteStorage
open Fable.ReactNative
open Fable.ReactNativeFileSystem

type AudioRepo =
    { Database: ISqLiteDatabase
      RootDirectoryPaths: seq<string>
      DbName: string }

let private initDirectoryTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Directory (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    DirectoryId INTEGER)"
    |> ignore
    debug "initalized Directory table"

let private initArtistTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Artist (
    Id INTEGER PRIMARY KEY,
    Name TEXT)"
    |> ignore
    debug "initalized Artist table"

let private initAlbumTable (tx: ISqLiteTransaction) =
    tx.ExecuteSql "CREATE TABLE IF NOT EXISTS Album (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    NumTrack INTEGER,
    ArtistId INTEGER,
    Cover BLOB)"
    |> ignore
    debug "initalized Album table"

let private initTrackTable (tx: ISqLiteTransaction) =
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
    debug "initalized Track table"

// let private int option findTrackByPath (tx: ISqLiteTransaction) (path: string) =
//     let parts = path.Split("/")
//     let filename = Seq.last parts
//     let directories = Seq.take (parts.Length - 1) parts
//     let (_, result) = tx.ExecuteSql("SELECT Id, Directory FROM Track WHERE Filename = ?", [|filename|])
//     if result.Rows.Length = 0 then None else

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
        |> Array.map (fun path -> stat path |> Promise.map (fun statResult -> (path, statResult)))
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
                Promise.lift repo)))
