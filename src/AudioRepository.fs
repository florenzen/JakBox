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
//open Fable.Import.ReactNative.SqLiteStorage
//open Fable.ReactNative.SqLiteStorage
//open Fable.ReactNative.SqLiteStorageExtensions
open Fable.Core
open Fable.ReactNativeSqlite
open Fable.ReactNative
open Fable.ReactNativeFileSystem

type private DbParams(name: string,
                      ?location: Location,
                      ?createFromLocation: U2<float, string> option,
                      ?key: string option,
                      ?readOnly: bool option) =
    interface DatabaseParams with
        member val createFromLocation: U2<float, string> option = defaultArg createFromLocation None with get, set
        member val key: string option = defaultArg key None with get, set
        member val readOnly: bool option = defaultArg readOnly None with get, set
        member val name: string = name with get, set
        member val location: Location = defaultArg location Location.Default with get, set

type AudioRepo =
    { Database: SQLiteDatabase
      DbName: string }

let private initDirectoryTable (tx: Transaction) =
    let result =
        tx.executeSql "CREATE TABLE IF NOT EXISTS Directory (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    DirectoryId INTEGER)"

    debug "initalized Directory table"
    result

let private initArtistTable (tx: Transaction) =
    let result =
        tx.executeSql "CREATE TABLE IF NOT EXISTS Artist (
    Id INTEGER PRIMARY KEY,
    Name TEXT)"

    debug "initalized Artist table"
    result

let private initAlbumTable (tx: Transaction) =
    let result =
        tx.executeSql "CREATE TABLE IF NOT EXISTS Album (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    NumTrack INTEGER,
    ArtistId INTEGER,
    Cover BLOB)"

    debug "initalized Album table"
    result

let private initTrackTable (tx: Transaction) =
    let result =
        tx.executeSql "CREATE TABLE IF NOT EXISTS Track (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    AlbumnId INTEGER,
    TrackNumber INTEGER,
    Duration INTEGER,
    Filename TEXT,
    DirectoryId INTEGER,
    LastModified INTEGER)"

    debug "initalized Track table"
    result

let private initTables (db: SQLiteDatabase) =
    db.transaction (fun tx ->
        initDirectoryTable tx |> ignore
        initArtistTable tx |> ignore
        initAlbumTable tx |> ignore
        initTrackTable tx |> ignore)

let findAllAudioFiles () =
    getAll (GetAllOptions())
    |> Promise.map (fun tracks ->
        tracks
        |> Array.map (fun track -> track.Path)
        |> List.ofArray)

let openRepo (dbName: string) =
    SQLite.openDatabase (DbParams(dbName))
    |> Promise.bind (fun db ->
        debug "opened repo database %s" dbName
        initTables db
        |> Promise.map (fun _ -> { Database = db; DbName = dbName }))

let closeRepo (repo: AudioRepo) =
    repo.Database.close ()
    |> Promise.map (fun () -> debug "closed repo database %s" repo.DbName)

let statList (paths: string list) = paths |> List.map stat |> Promise.all

let updateRepo (repo: AudioRepo) =
    findAllAudioFiles ()
    |> Promise.bind (fun paths ->
        let path = List.head paths
        JsMediaTags.read path
        |> Promise.bind (fun id3 ->
            debug "ID3: %s %s %s" id3.Tags.Artist id3.Tags.Album id3.Tags.Title
            Fable.ReactNativeFileSystem.ReactNativeFileSystem.stat path
            |> Promise.bind (fun stat ->
                debug "size: %i, mtime: %s, ctime: %s" stat.Size (stat.Mtime.ToString()) (stat.Ctime.ToString())
                Promise.lift repo)))

SQLite.enablePromise true
#if DEBUG
SQLite.DEBUG true
#endif
