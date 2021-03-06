module JsonStream.Strings

open JsonStream.RState
open JsonStream.StateOps
open JsonStream.Character
open JsonStream.Types
open System.Text

let unicodeEsc =
  rstate {
    let! a = expectWith hexChar
    let! b = expectWith hexChar
    let! c = expectWith hexChar
    let! d = expectWith hexChar
    let num =
      (fromHex a.Val <<< 12) +
      (fromHex b.Val <<< 8)  +
      (fromHex c.Val <<< 4)  +
      (fromHex d.Val)
    return { Line = a.Line; Column = a.Column; Val = char num; }
  }

let unicodeSequence =
  rstate {
    let! char1 = unicodeEsc
    if isSurrogateChar char1.Val then
      do! expectN [ '\\'; 'u'; ]
      let! char2 = unicodeEsc
      if isSurrogateChar char2.Val then
        return {
          Line   = char1.Line;
          Column = char1.Column;
          Val    = sprintf "%c%c" char1.Val char2.Val
        }
      else
        return! unexpectedInput char2 |> fail
    else
      return {
        Line   = char1.Line;
        Column = char1.Column;
        Val    = char1.Val.ToString()
      }
  }

let esc =
  let fromChar jc c =
      { Line = jc.Line; Column = jc.Column; Val = c; }

  rstate {
    let! jv = next
    return!
      match jv.Val with
      | 'b' -> fromChar jv "\b" |> unit
      | 'f' -> fromChar jv "\f" |> unit
      | 'n' -> fromChar jv "\n" |> unit
      | 'r' -> fromChar jv "\r" |> unit
      | 't' -> fromChar jv "\t" |> unit
      | '"'
      | '\\'
      | '/' -> unit { Line = jv.Line; Column = jv.Column; Val = jv.Val.ToString() }
      | 'u' -> unicodeSequence
      | _   -> unexpectedInput jv |> fail
  }

let rec stringToken (builder: StringBuilder) =
  rstate {
    let! nc = next
    match nc.Val with
    | '\\' ->
      let! s = esc
      builder.Append(s.Val) |> ignore
      return! stringToken builder
    | '"' ->
      return Token.String (builder.ToString())
    | c when unescaped c ->
      builder.Append(c) |> ignore
      return! stringToken builder
    | _ ->
      return! unexpectedInput nc |> fail
  }
