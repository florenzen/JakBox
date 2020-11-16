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

module View

open Fable.ReactNative
open Fable.ReactNative.Props

open Model

let mainBackground = "#2b5079"
let currentTrackColor = "#aa1018"

let textPadding = Padding(dip 3.0)

let private debugBox (model: Model) =
    let box (content: string) (color: string) =
        text
            [ TextProperties.Style [ Width(pct 50.0)
                                     BackgroundColor(color)
                                     textPadding ] ]
            content

    view [ ViewProperties.Style [ FlexDirection FlexDirection.Row ] ] [
        box model.PreviousAction "#119900"
        box model.NextAction "#991100"
    ]

let artistBox (model: Model) =
    view [] [
        text [ TextProperties.Style [ textPadding ] ] (if model.CurrentArtist = "" then "--" else model.CurrentArtist)
    ]

let albumBox (model: Model) =
    view [] [
        text [ TextProperties.Style [ textPadding ] ] (if model.CurrentAlbum = "" then "--" else model.CurrentAlbum)
    ]

let coverBox (model: Model) =
    view [] [
        text
            [ TextProperties.Style [ Width(pct 100.0)
                                     BackgroundColor("#999999") ] ]
            (if model.CurrentTrack = "" then "--" else model.CurrentTrack)
    ]

let progressSlider (model: Model) =
    slider [ SliderProperties.Value(model.CurrentPosition) ]

let trackButtons (model: Model) =
    let button number active =
        let properties =
            if active
            then [ ViewProperties.Style [ BackgroundColor(currentTrackColor) ] ]
            else []
        let title = number.ToString()

        view
            properties
            [ button [ButtonProperties.Title(title)] [
                text [] title
              ] ]

    let buttons =
        [ for num in 1 .. model.NumberOfTracks -> button num (num = model.CurrentTrackNumber) ]

    view [] buttons

let view (model: Model) dispatch =
    view [ ViewProperties.Style [ FlexDirection(FlexDirection.Column)
                                  BackgroundColor(mainBackground) ] ] [
        artistBox model
        albumBox model
        coverBox model
        progressSlider model
        trackButtons model
        debugBox model
    ]
