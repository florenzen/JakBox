module JakBox

open Elmish
open Elmish.React
open Elmish.ReactNative
open Fable.ReactNative

// A very simple app which increments a number when you press a button

type Model = ()

type Message = ()

let init () = ((), Cmd.none)

let update msg model = ((), Cmd.none)
  
let view model dispatch = 
    view [] [text [] "JakBox" ]

Program.mkProgram init update view
#if RELEASE
#else
|> Program.withConsoleTrace
#endif
|> Program.withReactNative "JakBox"
|> Program.run
