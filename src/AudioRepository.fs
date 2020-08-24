module AudioRepository

open Fable.ReactNative.AndroidAudioStore

let findAllMusicFiles () =
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
