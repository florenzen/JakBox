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
open Fable.ReactNative.SqLiteStorageExtensions
open Fable.ReactNative

type AudioRepo =
    { Database: ISqLiteDatabase
      DbName: string }

let private initDirectoryTable (tx: ISqLiteTransaction) =
    tx.ExecuteNonQuery "CREATE TABLE IF NOT EXISTS Directory (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    DirectoryId INTEGER)"
    |> Promise.map (fun () -> debug "initalized Directory table")

let private initArtistTable (tx: ISqLiteTransaction) =
    tx.ExecuteNonQuery "CREATE TABLE IF NOT EXISTS Artist (
    Id INTEGER PRIMARY KEY,
    Name TEXT)"
    |> Promise.map (fun () -> debug "initalized Artist table")

let private initAlbumTable (tx: ISqLiteTransaction) =
    tx.ExecuteNonQuery "CREATE TABLE IF NOT EXISTS Album (
    Id INTEGER PRIMARY KEY,
    Name,
    ArtistId INTEGER,
    Cover BLOB)"
    |> Promise.map (fun () -> debug "initalized Album table")

let private initTrackTable (tx: ISqLiteTransaction) =
    tx.ExecuteNonQuery "CREATE TABLE IF NOT EXISTS Track (
    Id INTEGER PRIMARY KEY,
    Name TEXT,
    AlbumnId INTEGER,
    Duration INTEGER,
    Filename TEXT,
    DirectoryId INTEGER)"
    |> Promise.map (fun () -> debug "initalized Track table")

let private initTables (db: ISqLiteDatabase) =
    db.Transaction(fun tx ->
        initDirectoryTable tx |> ignore
        initArtistTable tx |> ignore
        initAlbumTable tx |> ignore
        initTrackTable tx)

let findAllAudioFiles () =
    getAll
        { id = false
          blured = false
          artist = false
          duration = false
          cover = false
          genre = false
          title = false
          minimumSongDuration = 10u }
    |> Promise.map (fun tracks ->
        tracks
        |> Array.map (fun track -> track.path)
        |> List.ofArray)

let openRepo (dbName: string) =
#if DEBUG
    setDebugMode true
#endif
    openDatabase dbName
    |> Promise.bind (fun db ->
        debug "opened repo database %s" dbName
        initTables db
        |> Promise.map (fun () -> { Database = db; DbName = dbName }))

let closeRepo (repo: AudioRepo) =
    repo.Database.CloseDatabase()
    debug "closed repo database %s" repo.DbName

let updateRepo (repo: AudioRepo) =
    findAllAudioFiles ()
    |> Promise.bind (fun paths ->
        let path = List.head paths
        JsMediaTags.read path
        |> Promise.bind (fun id3 ->
            debug "ID3: %s %s %s" id3.Tags.Artist id3.Tags.Album id3.Tags.Title
            Promise.lift repo))
