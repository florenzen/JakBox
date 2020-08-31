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

namespace Fable.Import.ReactNative.SqLiteStorage

open Fable.Core

// fsharplint:disable MemberNames
type ISqLiteTransaction =
    abstract executeSql: string * obj [] -> JS.Promise<obj []>

type ISqLiteDatabase =
    abstract closeDatabase: unit -> JS.Promise<unit>
    abstract transaction: (ISqLiteTransaction -> 'T) -> JS.Promise<'T>

type ISqLite =
    abstract openDatabase: string -> JS.Promise<ISqLiteDatabase>
    abstract enablePromise: bool -> unit
    abstract DEBUG: bool -> unit
// fsharp:enable MemberNames


namespace Fable.ReactNative.SqLiteStorage

open Fable.Import.ReactNative.SqLiteStorage
open Fable.Core.JsInterop

type SqLiteTransaction(transaction: ISqLiteTransaction) =
    member __.ExecuteSql(statement: string, ?values: seq<obj>) =
        let valuesArray =
            defaultArg values Seq.empty |> Seq.toArray

        transaction.executeSql (statement, valuesArray)

type SqLiteDatabase(path: string) =
    let sqLite: ISqLite =
        importDefault "react-native-sqlite-storage"

    do
        printf "%O" sqLite
        sqLite.DEBUG true
        sqLite.enablePromise true
        printf "enabled promise"

    let mutable sqLiteDatabase: ISqLiteDatabase option = None

    member __.OpenDatabase() =
        sqLite.openDatabase path
        |> Promise.map (fun db -> sqLiteDatabase <- Some db)

    member __.CloseDatabase() =
        match sqLiteDatabase with
        | None -> Promise.lift ()
        | Some db ->
            db.closeDatabase ()
            |> Promise.map (fun _ -> sqLiteDatabase <- None)

    member __.Transaction(operation: SqLiteTransaction -> 'T) =
        match sqLiteDatabase with
        | None -> Promise.reject (sprintf "SQLite database %s not opened" path)
        | Some db -> db.transaction (SqLiteTransaction >> operation)
