module JsonStream.Tests.TokenizerTests

open Expecto
open FSharpx.Collections
open JsonStream.Tokenizer
open JsonStream.Types

let singletonList f xs =
  match LazyList.length xs with
  | 1 ->
    match LazyList.head xs with
    | Ok x when f x.Val -> true
    | _ -> false
  | _ -> false

let makeTest (input, expected) =
  testCase (sprintf "Tokenizes %s" input) <| fun _ ->
    let result = LazyList.ofSeq input |> tokenize
    let isOk x = x = expected
    Expect.isTrue (singletonList isOk result) (sprintf "Failed to tokenize \"%s\"" input)

let inputs = [
  ("null", Token.Null)
  ("true", Token.True)
  ("false", Token.False)
  ("{", Token.LeftCurly)
  ("}", Token.RightCurly)
  ("[", Token.LeftBracket)
  ("]", Token.RightBracket)
  (":", Token.Colon)
  (",", Token.Comma)
]

[<Tests>]
let tests =
  testList "Tokenizer" [
    testList "Scalar Tokens" <| List.map makeTest inputs

    testCase "Tokenizes empty stream" <| fun _ ->
      let subject = LazyList.empty |> tokenize
      Expect.isTrue (LazyList.isEmpty subject) "Failed to tokenize an empty stream"
  ]
