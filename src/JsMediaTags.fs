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

module JsMediaTags =
    open Fable.Core
    open Fable.Core.JsInterop

    type IPicture =
        [<Emit("$0.data")>]
        abstract Data: byte []

        [<Emit("$0.description")>]
        abstract Description: string

        [<Emit("$0.format")>]
        abstract Format: string

        [<Emit("$0.type")>]
        abstract Type: string

    type ITags =
        [<Emit("$0.artist")>]
        abstract Artist: string

        [<Emit("$0.album")>]
        abstract Album: string

        [<Emit("$0.title")>]
        abstract Title: string

        [<Emit("$0.track")>]
        abstract Track: string

        [<Emit("$0.genre")>]
        abstract Genre: string

        [<Emit("$0.year")>]
        abstract Year: string

        [<Emit("$0.picture")>]
        abstract Picture: IPicture

    type ITag =
        [<Emit("$0.type")>]
        abstract Type: string

        [<Emit("$0.version")>]
        abstract Version: string

        [<Emit("$0.major")>]
        abstract Major: int32

        [<Emit("$0.revision")>]
        abstract Revision: int32

        [<Emit("$0.size")>]
        abstract Size: int32

        [<Emit("$0.tags")>]
        abstract Tags: ITags

    type private Callbacks(onSuccess: ITag -> unit, onError: exn -> unit) =
        [<Emit("$0.onSuccess")>]
        member __.OnSuccess = onSuccess

        [<Emit("$0.onError")>]
        member __.OnError = onError

    type private IReader =
        [<Emit("$0.read($1)")>]
        abstract Read: Callbacks -> unit

        [<Emit("$0.setTagsToRead($1)")>]
        abstract SetTagsToRead: string [] -> IReader

    type private IJsMediaTags =
        [<Emit("new $0.Reader($1)")>]
        abstract Reader: string -> IReader

    let private jsMediaTags: IJsMediaTags = importDefault "jsmediatags"

    type Tag =
        | Title
        | Artist
        | Album
        | Year
        | Comment
        | Track
        | Genre
        | Picture
        | Lyrics
        override this.ToString() =
            match this with
            | Title -> "title"
            | Artist -> "artist"
            | Album -> "album"
            | Year -> "year"
            | Comment -> "comment"
            | Track -> "track"
            | Genre -> "genre"
            | Picture -> "picture"
            | Lyrics -> "lyrics"

    let read (path: string) =
        Promise.create (fun resolve reject ->
            let reader = jsMediaTags.Reader(path)
            reader.Read(Callbacks(resolve, reject)))

    let readTags (path: string) (tags: seq<Tag>) =
        Promise.create (fun resolve reject ->
            let reader = jsMediaTags.Reader(path)

            let tagStrings =
                tags
                |> Seq.map (fun tag -> tag.ToString())
                |> Seq.toArray

            reader.SetTagsToRead(tagStrings).Read(Callbacks(resolve, reject)))
