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

module JakBox

open Elmish
open Elmish.React
open Elmish.ReactNative
open Fable.ReactNative


open Utils
open Fable.Import.ReactNative.SqLiteStorage
open Fable.Core.JsInterop

type Model = { Text: string }

type Message =
    | RequestPermissionResult of Permissions.PermissionStatus
    | FindAllAudioFilesResult of string list
    | InteractWithSqLiteResult of string

let requestReadExternalStoragePermission () =
    Permissions.check Permissions.Android.ReadExternalStorage
    |> Promise.bind (fun result ->
        if result <> Permissions.Granted then
            debug "current permission status not %O, requesting it" Permissions.Granted

            let requestResult =
                Permissions.request Permissions.Android.ReadExternalStorage

            debug "result of requesting permission %O" requestResult
            requestResult
            |> Promise.map RequestPermissionResult
        else
            debug "permission already granted"
            RequestPermissionResult result |> Promise.lift)

let findAllAudioFiles () =
    AudioRepository.findAllAudioFiles ()
    |> Promise.map FindAllAudioFilesResult


// let sqLite: ISqLite =
//     importDefault "react-native-sqlite-storage"

let interactWithSqLite () =
    // sqLite.EnablePromise (true)

    // sqLite.Debug(true)
    SqLiteStorage.openDatabase "repo.sqlite"
    |> Promise.bind (fun db ->
        db.Transaction (fun tx ->
            tx.ExecuteSql ("CREATE TABLE IF NOT EXISTS Foo (Id INT, Name TEXT)", [||])
            tx.ExecuteSql ("INSERT INTO Foo (Id, Name) VALUES (1, 'one111')", [||])
            tx.ExecuteSql ("SELECT * FROM Foo", [||]) 
            |> Promise.bind (fun r ->
                // |> Promise.bind (fun r2 ->
                printfn "foo %O" (r.[1].Rows.Item(4))?Name
                printfn "len %O" (r.[1].Rows.Length)
                Promise.lift r.[0].Rows))
                
                )

                // |> Promise.bind (fun rows2 ->
                //     tx.ExecuteSql ("SELECT * FROM Foo", [||])
                //     |> Promise.bind (fun r4 ->
                //         printfn "bar %O" (r4.[1].Rows.Item(1)?Id)
                //         Promise.lift ())))))

    |> Promise.bind (fun r2 ->Promise.lift (InteractWithSqLiteResult "finished with SQLite"))


// let sqLite = SqLiteDatabase("repo.sqlite")
// sqLite.OpenDatabase()
// |> Promise.bind (fun _ ->
//     // let tx = sqLite.Transaction()
//     // printfn "sqLite %O" sqLite
//     // printfn "tx1 %O" tx
//     sqLite.Transaction(fun tx ->
//         printfn "tx %O" tx
//         tx.ExecuteSql("CREATE TABLE IF NOT EXISTS Foo (Id INT, Name TEXT)")
//         |> Promise.bind (fun (tx1, r1) ->
//             printfn "result of create table %O" r1.Length
//             tx1.ExecuteSql("INSERT INTO Foo (Id, Name) VALUES (1, 'one')")
//             |> Promise.bind (fun r2 ->
//             //     tx.ExecuteSql("INSERT INTO Foo (Id, Name) VALUES (2, 'two')")
//             //     |> Promise.bind (fun r3 ->
//             //         tx.ExecuteSql("SELECT * FROM Foo")
//             //         |> Promise.bind (fun res ->
//             //             printfn "select result %O" res
//                 Promise.lift (InteractWithSqLiteResult "created table Foo")))))

let init () =
    let initModel = { Text = "initialized" }
    let initSteps = requestReadExternalStoragePermission ()
    (initModel, Cmd.OfPromise.result initSteps)

let update msg model =
    match msg with
    | RequestPermissionResult result ->
        //let nextStep = findAllAudioFiles ()
        let nextStep = interactWithSqLite ()
        ({ Text = sprintf "result of permission request: %O" result }, Cmd.OfPromise.result nextStep)
    | FindAllAudioFilesResult paths ->
        let fileListing = String.concat "\n" paths
        ({ Text = fileListing }, Cmd.none)
    | InteractWithSqLiteResult text -> ({ Text = text }, Cmd.none)

let view model dispatch = view [] [ text [] model.Text ]

Program.mkProgram init update view
#if RELEASE
#else
|> Program.withConsoleTrace
#endif
|> Program.withReactNative "JakBox"
|> Program.run
