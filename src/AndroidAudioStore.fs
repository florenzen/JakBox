namespace Fable.ReactNative.AndroidAudioStore

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import.Browser
module Store =
    type GetAllOptions =
        { id: bool
          blured: bool
          artist: bool
          duration: bool
          cover: bool
          genre: bool
          title: bool
          minimumSongDuration: uint32 }

    type Track =
        { id: string
          title: string
          author: string
          album: string
          genre: string
          duration: string
          cover: string
          path: string }


    type TrackExtended =
        class
        end

    type IMusicFiles =
        //abstract getAll: Options -> JS.Promise<TrackExtended[]>
        abstract getAll: GetAllOptions -> JS.Promise<Track []>

    // type Store() =
    //[<ImportDefault("react-native-get-music-files")>]
    let rngmf: IMusicFiles =
        //jsNative
        importDefault "react-native-get-music-files"

    let musicFiles: IMusicFiles = 
        printf "rngmf %O" rngmf        
        rngmf

    //  member __.GetSongs(?id: bool, ?blured: bool, ?artist: bool, ?duration: bool, ?cover: bool, ?genre: bool,
    //                            ?title: bool, ?minimumSongDuration: uint32) =
    let GetSongs ()
        =
        musicFiles.getAll 
            { id = true
              blured = false
              artist = true
              duration = true
              cover = true
              genre = true
              title = true
              minimumSongDuration = 1000u }
            // { id = defaultArg id true
            //   blured = defaultArg blured false
            //   artist = defaultArg artist true
            //   duration = defaultArg duration true
            //   cover = defaultArg cover true
            //   genre = defaultArg genre true
            //   title = defaultArg genre true
            //   minimumSongDuration = defaultArg minimumSongDuration 1000u }
