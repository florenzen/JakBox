namespace Fable.ReactNative

open Fable.Core
open Fable.Core.JsInterop

module Permissions =
    let private reactNativePermissions: obj = importAll "react-native-permissions"

    [<AbstractClass>]
    type Permission =
        class
        end

    type Android() =
        static let permissions =
            reactNativePermissions?PERMISSIONS?ANDROID

        static let asPermission (jsPermission: obj) = jsPermission :?> Permission

        static member AcceptHandover = permissions?ACCEPT_HANDOVER |> asPermission
        static member AccessBackgroundLocation = permissions?ACCESS_BACKGROUND_LOCATION |> asPermission
        static member AccessCoarseLocation = permissions?ACCESS_COARSE_LOCATION |> asPermission
        static member AccessFineLocation = permissions?ACCESS_FINE_LOCATION |> asPermission
        static member ActivityRecognition = permissions?ACTIVITY_RECOGNITION |> asPermission
        static member AddVoicemail = permissions?ADD_VOICEMAIL |> asPermission
        static member AnswerPhoneCalls = permissions?ANSWER_PHONE_CALLS |> asPermission
        static member BodySensors = permissions?BODY_SENSORS |> asPermission
        static member CallPhone = permissions?CALL_PHONE |> asPermission
        static member Camera = permissions?CAMERA |> asPermission
        static member GetAccounts = permissions?GET_ACCOUNTS |> asPermission
        static member ProcessOutgoingCalls = permissions?PROCESS_OUTGOING_CALLS |> asPermission
        static member ReadCalendar = permissions?READ_CALENDAR |> asPermission
        static member ReadContacts = permissions?READ_CONTACTS |> asPermission
        static member ReadPhoneNumbers = permissions?READ_PHONE_NUMBERS |> asPermission
        static member ReadPhoneState = permissions?READ_PHONE_STATE |> asPermission
        static member ReadSms = permissions?READ_SMS |> asPermission
        static member ReceiveMms = permissions?RECEIVE_MMS |> asPermission
        static member ReceiveSms = permissions?RECEIVE_SMS |> asPermission
        static member ReceiveWapPush = permissions?RECEIVE_WAP_PUSH |> asPermission
        static member RecordAudio = permissions?RECORD_AUDIO |> asPermission
        static member SendSms = permissions?SEND_SMS |> asPermission
        static member UseSip = permissions?USE_SIP |> asPermission
        static member WriteCalendar = permissions?WRITE_CALENDAR |> asPermission
        static member WriteCallLog = permissions?WRITE_CALL_LOG |> asPermission
        static member WriteContacts = permissions?WRITE_CONTACTS |> asPermission
        static member WriteExternalStorage = permissions?WRITE_EXTERNAL_STORAGE |> asPermission
        static member ReadExternalStorage = permissions?READ_EXTERNAL_STORAGE |> asPermission

    type PermissionStatus =
        | Unavailable
        | Denied
        | Blocked
        | Granted

    let private resultToPermissionStatus (result: obj) =
        let results = reactNativePermissions?RESULTS
        if result = results ?UNAVAILABLE then Unavailable
        else if result = results ?DENIED then Denied
        else if result = results ?BLOCKED then Blocked
        else Granted

    let check (permission: Permission): JS.Promise<PermissionStatus> =
        reactNativePermissions?check (permission)
        |> Promise.map resultToPermissionStatus

    let request (permission: Permission): JS.Promise<PermissionStatus> =
        reactNativePermissions?request (permission)
        |> Promise.map resultToPermissionStatus