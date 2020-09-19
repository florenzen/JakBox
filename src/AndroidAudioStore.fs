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

    type GetAllOptions =
        { [<Emit("$0.id")>]
          Id: bool
          [<Emit("$0.blured")>]
          Blured: bool
          [<Emit("$0.artist")>]
          Artist: bool
          [<Emit("$0.duration")>]
          Duration: bool
          [<Emit("$0.cover")>]
          Cover: bool
          [<Emit("$0.genre")>]
          Genre: bool
          [<Emit("$0.title")>]
          Title: bool
          [<Emit("$0.minimumSongDuration")>]
          MinimumSongDuration: uint32 }

    type Track =
        { [<Emit("$0.id")>]
          Id: string
          [<Emit("$0.title")>]
          Title: string
          [<Emit("$0.author")>]
          Author: string
          [<Emit("$0.album")>]
          Album: string
          [<Emit("$0.genre")>]
          Genre: string
          [<Emit("$0.duration")>]
          Duration: string
          [<Emit("$0.cover")>]
          Cover: string
          [<Emit("$0.path")>]
          Path: string }

    type private IMusicFiles =
        [<Emit("$0.getAll($1)")>]
        abstract GetAll: GetAllOptions -> JS.Promise<Track []>

    let private reactNativeGetMusicFiles: IMusicFiles =
        importDefault "react-native-get-music-files"

    let getAll (options: GetAllOptions) = reactNativeGetMusicFiles.GetAll options
