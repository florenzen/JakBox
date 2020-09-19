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

namespace Fable.ReactNativeFileSystem

open System
open Fable.Core.JsInterop
open Fable.Core

[<AutoOpen>]
module ReactNativeFileSystem =

    [<Import("default", "react-native-fs")>]
    let private fileSystem = obj ()

    type IStatResult =
        [<Emit("$0.path")>]
        abstract Path: string

        [<Emit("$0.ctime")>]
        abstract Ctime: DateTime

        [<Emit("$0.mtime")>]
        abstract Mtime: DateTime

        [<Emit("$0.size")>]
        abstract Size: uint32

        [<Emit("$0.mode")>]
        abstract Mode: uint32

        [<Emit("$0.originalFilepath")>]
        abstract OriginalFilepath: string

        [<Emit("$0.isFile")>]
        abstract IsFile: unit -> bool

        [<Emit("$0.isDirectory")>]
        abstract IsDirectory: unit -> bool

    let stat (uri: string): JS.Promise<IStatResult> = fileSystem?stat uri
