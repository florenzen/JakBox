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

type ISqLiteRows =
    [<Emit "$0.item($1)">]
    abstract Item: int32 -> obj

    [<Emit "$0.length">]
    abstract Length: int32

type ISqLiteResult =
    [<Emit "$0.rows">]
    abstract Rows: ISqLiteRows

type ISqLiteTransaction =
    [<Emit "$0.executeSql($1, $2)">]
    abstract ExecuteSql: string * ?args: obj [] -> JS.Promise<ISqLiteTransaction * ISqLiteResult>

type ISqLiteDatabase =
    [<Emit "$0.close($1)">]
    abstract Close: unit -> JS.Promise<unit>

    [<Emit "$0.transaction($1)">]
    abstract Transaction: (ISqLiteTransaction -> unit) -> JS.Promise<ISqLiteTransaction>

    [<Emit "$0.executeSql($1, $2)">]
    abstract ExecuteSql: string * ?args: obj [] -> JS.Promise<obj []>

type ISqLite =
    [<Emit "$0.openDatabase($1)">]
    abstract OpenDatabase: string -> JS.Promise<ISqLiteDatabase>

    [<Emit "$0.enablePromise($1)">]
    abstract EnablePromise: bool -> unit

    [<Emit "$0.DEBUG($1)">]
    abstract Debug: bool -> unit

namespace Fable.ReactNative

open Fable.Import.ReactNative.SqLiteStorage
open Fable.Core.JsInterop

module SqLiteStorage =
    let private sqLite: ISqLite =
        importDefault "react-native-sqlite-storage"

    let openDatabase (path: string) = sqLite.OpenDatabase path

    let setDebugMode (enable: bool) = sqLite.Debug enable

    sqLite.EnablePromise true
