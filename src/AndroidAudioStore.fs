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

namespace Fable.ReactNative

module AndroidAudioStore =
    open Fable.Core
    open Fable.Core.JsInterop

    type GetAllOptions(?id: bool,
                       ?blured: bool,
                       ?artist: bool,
                       ?duration: bool,
                       ?cover: bool,
                       ?genre: bool,
                       ?title: bool,
                       ?minimumSongDuration: uint32) =
        [<Emit("$0.id")>]
        member __.Id: bool = defaultArg id false

        [<Emit("$0.blured")>]
        member __.Blured: bool = defaultArg blured false

        [<Emit("$0.artist")>]
        member __.Artist: bool = defaultArg artist false

        [<Emit("$0.duration")>]
        member __.Duration: bool = defaultArg duration false

        [<Emit("$0.cover")>]
        member __.Cover: bool = defaultArg cover false

        [<Emit("$0.genre")>]
        member __.Genre: bool = defaultArg genre false

        [<Emit("$0.title")>]
        member __.Title: bool = defaultArg title false

        [<Emit("$0.minimumSongDuration")>]
        member __.MinimumSongDuration: uint32 = defaultArg minimumSongDuration 10u

    type ITrack =
        [<Emit("$0.id")>]
        abstract Id: string

        [<Emit("$0.title")>]
        abstract Title: string

        [<Emit("$0.author")>]
        abstract Author: string

        [<Emit("$0.album")>]
        abstract Album: string

        [<Emit("$0.genre")>]
        abstract Genre: string

        [<Emit("$0.duration")>]
        abstract Duration: string

        [<Emit("$0.cover")>]
        abstract Cover: string

        [<Emit("$0.path")>]
        abstract Path: string

    type private IMusicFiles =
        [<Emit("$0.getAll($1)")>]
        abstract GetAll: GetAllOptions -> JS.Promise<ITrack []>

    let private reactNativeGetMusicFiles: IMusicFiles =
        importDefault "react-native-get-music-files"

    let getAll (options: GetAllOptions) = reactNativeGetMusicFiles.GetAll options
