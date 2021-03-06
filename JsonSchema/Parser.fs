module rec JsonSchema.Parser

open JsonDeserializer.Types
open JsonSchema.Types
open System.Text.RegularExpressions
open System.Collections.Generic

let (<!>) = Result.map

let scalarTypeConstraint = function
| JsonNode.String "null"    -> ScalarType.Null    |> Ok
| JsonNode.String "boolean" -> ScalarType.Boolean |> Ok
| JsonNode.String "object"  -> ScalarType.Object  |> Ok
| JsonNode.String "array"   -> ScalarType.Array   |> Ok
| JsonNode.String "number"  -> ScalarType.Number  |> Ok
| JsonNode.String "string"  -> ScalarType.String  |> Ok
| JsonNode.String "integer" -> ScalarType.Integer |> Ok
| _                         -> Error "Invalid type constraint"

let rec foldResults list acc =
  match List.tryHead list with
  | Some (Ok x)->
    (fun xs -> x :: xs) <!> acc |> foldResults (List.tail list)
  | Some (Error e) -> Error e
  | None -> acc

let rec makeTypeConstraint node =
  match node with
  | JsonNode.Array xs ->
    foldResults (List.map scalarTypeConstraint xs) (Ok List.empty)
    |> Result.map (ListType >> Assertion.Type)
  | JsonNode.String _ ->
    Result.map ScalarType (scalarTypeConstraint node)
    |> Result.map Assertion.Type
  | _ -> Error "Invalid type constraint"

let makeEnumConstraint node =
  match node with
  | JsonNode.Array xs ->
    xs |> Assertion.Enum |> Ok
  | _ -> Error "Invalid enum constraint"

let makeConstConstraint node =
  Assertion.Const node |> Ok

let strToInt x =
  let couldParse, parsed = System.Int64.TryParse x
  match couldParse with
  | true  -> SchemaNumber.Integer parsed |> Ok
  | false -> sprintf "Unable to parse integer from %s" x |> Error

let strToUint x =
  let couldParse, parsed = System.UInt64.TryParse x
  match couldParse with
  | true  -> Ok parsed
  | false -> sprintf "Unable to parse unsigned integer from %s" x |> Error

let strToFloat x =
  let couldParse, parsed = System.Double.TryParse x
  match couldParse with
  | true -> SchemaNumber.Double parsed |> Ok
  | false -> sprintf "Unable to parse number from %s" x |> Error

let strToNum (x: string) =
  if x.Contains "." then
    strToFloat x
  else
    strToInt x

let makeMultipleOfConstraint = function
| JsonNode.Number n -> strToNum n |> Result.map Assertion.MultipleOf
| _ -> Error "Invalid multipleOf constraint"

let makeMaximumConstraint = function
| JsonNode.Number n -> strToInt n |> Result.map Assertion.Maximum
| _ -> Error "Invalid maximum constraint"

let makeMinimumConstraint = function
| JsonNode.Number n -> strToInt n |> Result.map Assertion.Minimum
| _ -> Error "Invalid minimum constraint"

let makeExclusiveMaximumConstraint = function
| JsonNode.Number n -> strToInt n |> Result.map Assertion.ExclusiveMaximum
| _ -> Error "Invalid exclusiveMaximumConstraint"

let makeExclusiveMinimumConstraint = function
| JsonNode.Number n -> strToInt n |> Result.map Assertion.ExclusiveMinimum
| _ -> Error "Invalid exclusiveMinimumConstraint"

let makeMaxLengthConstraint = function
| JsonNode.Number n -> strToUint n |> Result.map Assertion.MaxLength
| _ -> Error "Invalid maxLength"

let makeMinLengthConstraint = function
| JsonNode.Number n -> strToUint n |> Result.map Assertion.MinLength
| _ -> Error "Invalid minLength"

let makeAdditionalItemsConstraint node =
  Result.map Assertion.AdditionalItems (parse node)

let makeMaxItemsConstraint = function
| JsonNode.Number n -> strToUint n |> Result.map Assertion.MaxItems
| _ -> Error "Invalid maxItems"

let makeMinItemsConstraint = function
| JsonNode.Number n -> strToUint n |> Result.map Assertion.MinItems
| _ -> Error "Invalid minItems"

let makeUniqueItemsConstraint = function
| JsonNode.Boolean b -> b |> Assertion.UniqueItems |> Ok
| _ -> Error "Invalid uniqueItems"

let makeContainsConstraint node =
  Result.map Assertion.Contains (parse node)

let makeMaxPropertiesConstraint = function
| JsonNode.Number n -> strToUint n |> Result.map Assertion.MaxProperties
| _ -> Error "Invalid maxProperties"

let makeMinPropertiesConstraint = function
| JsonNode.Number n -> strToUint n |> Result.map Assertion.MinProperties
| _ -> Error "Invalid minProperties"

let makeRequiredConstraint = function
| JsonNode.Array xs ->
  // TODO unique
  List.fold (fun s x ->
    match x, s with
    | JsonNode.String s, Ok state -> Ok (s :: state)
    | _ -> Error "Invalid required"
  ) (Ok [ ]) xs |> Result.map Assertion.Required
| _ -> Error "Invalid required"

let makePropertiesConstraint = function
| JsonNode.Object m ->
  let folder s k v =
    match parse v, s with
    | Ok schema, Ok s -> Map.add k schema s |> Ok
    | _, Error e -> Error e
    | Error e, _ -> Error e
  Map.fold folder (Ok Map.empty) m |> Result.map Assertion.Properties
| _ -> Error "Invalid properties"

let makeAdditionalPropertiesConstraint node =
  Result.map Assertion.AdditionalProperties (parse node)

let makePropertyNamesConstraint node =
  Result.map Assertion.PropertyNames (parse node)

let makeItemsConstraint = function
| JsonNode.Array xs ->
  let folder s x =
    match s with
    | Error e -> Error e
    | Ok xs ->
      let schema = parse x
      Result.map (fun x -> x :: xs) schema
  let res = List.fold folder (Ok List.empty) xs
  Result.map (MultiJsonSchema >> Assertion.Items) res
| x ->
  let schema = parse x
  Result.map (SingletonItemSchema >> Assertion.Items) schema

let makeDependencyArray = function
| JsonNode.Array xs ->
  let folder state x =
    match x, state with
    | JsonNode.String s, Ok xs -> (Ok (s :: xs))
    | _ -> Error "Invalid dependency array item"
  let deps = List.fold folder (Ok List.empty) xs
  Result.map ArrayDependency deps
| _ -> Error "Invalid dependency array"

let makeSingletonDependency node =
  Result.map SchemaDependency (parse node)

let makeDependenciesConstraint = function
| JsonNode.Object m ->
  let folder s k v =
    match v, s with
    | JsonNode.Array _, Ok acc ->
      Result.map (fun x -> Map.add k x acc) (makeDependencyArray v)
    | x, Ok acc ->
      Result.map (fun x -> Map.add k x acc) (makeSingletonDependency x)
    | _ -> Error "Invalid dependency"
  let foo = Map.fold folder (Ok Map.empty) m
  Result.map Assertion.Depenedenciesof foo
| _ -> Error "Invalid dependencies"

let makePatternConstraint = function
| JsonNode.String s ->
  try
    new Regex(s, RegexOptions.ECMAScript) |> Assertion.Pattern |> Ok
  with
  | _ -> Error "Invalid regex for pattern"
| _ -> Error "Invalid pattern"

let makePatternPropertiesConstraint = function
| JsonNode.Object m ->
  let initState = new Dictionary<Regex, JsonSchema>() |> Ok
  let folder (s: Result<Dictionary<Regex, JsonSchema>, string>) k v =
    try
      let re = new Regex(k, RegexOptions.ECMAScript)
      let schema = parse v
      match schema, s with
      | Ok x, Ok y ->
        y.Add(re, x)
        Ok y
      | _, Error e -> Error e
      | Error e, _ -> Error e
    with
    | _ -> Error "Invalid regex for patternProperties"
  let res = Map.fold folder initState m
  Result.map Assertion.PatternProperties res
| _ -> Error "Invalid patternProperties"

let makeConstraint name node =
  match name with
  | "type"                 -> makeTypeConstraint node                 |> Some
  | "enum"                 -> makeEnumConstraint node                 |> Some
  | "const"                -> makeConstConstraint node                |> Some
  | "multipleOf"           -> makeMultipleOfConstraint node           |> Some
  | "maximum"              -> makeMaximumConstraint node              |> Some
  | "minimum"              -> makeMinimumConstraint node              |> Some
  | "exclusiveMaximum"     -> makeExclusiveMaximumConstraint node     |> Some
  | "exclusiveMinimum"     -> makeExclusiveMinimumConstraint node     |> Some
  | "maxLength"            -> makeMaxLengthConstraint node            |> Some
  | "minLength"            -> makeMinLengthConstraint node            |> Some
  | "pattern"              -> makePatternConstraint node              |> Some
  | "items"                -> makeItemsConstraint node                |> Some
  | "additionalItems"      -> makeAdditionalItemsConstraint node      |> Some
  | "maxItems"             -> makeMaxItemsConstraint node             |> Some
  | "minItems"             -> makeMinItemsConstraint node             |> Some
  | "uniqueItems"          -> makeUniqueItemsConstraint node          |> Some
  | "contains"             -> makeContainsConstraint node             |> Some
  | "maxProperties"        -> makeMaxPropertiesConstraint node        |> Some
  | "minProperties"        -> makeMinPropertiesConstraint node        |> Some
  | "required"             -> makeRequiredConstraint node             |> Some
  | "properties"           -> makePropertiesConstraint node           |> Some
  | "patternProperties"    -> makePatternPropertiesConstraint node    |> Some
  | "additionalProperties" -> makeAdditionalPropertiesConstraint node |> Some
  | "dependencies"         -> makeDependenciesConstraint node         |> Some
  | "propertyNames"        -> makePropertyNamesConstraint node        |> Some
  | _                      -> None

let makeTitleAnnotation = function
| JsonNode.String s -> s |> Annotation.Title |> Ok
| _ -> Error "Invalid title"

let makeDescriptionAnnotation = function
| JsonNode.String s -> s |> Annotation.Description |> Ok
| _ -> Error "Invalid description"

let makeDefaultAnnotation node =
  node |> Annotation.Default |> Ok

let makeReadOnlyAnnotation = function
| JsonNode.Boolean b -> b |> Annotation.ReadOnly |> Ok
| _ -> Error "Invalid readOnly"

let makeWriteOnlyAnnotation = function
| JsonNode.Boolean b -> b |> Annotation.WriteOnly |> Ok
| _ -> Error "Invalid writeOnly"

let makeExamplesAnnotation = function
| JsonNode.Array xs -> xs |> Annotation.Examples |> Ok
| _ -> Error "Invalid examples"

let makeAnnotation name node =
  match name with
  | "title"       -> makeTitleAnnotation node       |> Some
  | "description" -> makeDescriptionAnnotation node |> Some
  | "default"     -> makeDefaultAnnotation node     |> Some
  | "readOnly"    -> makeReadOnlyAnnotation node    |> Some
  | "writeOnly"   -> makeWriteOnlyAnnotation node   |> Some
  | "examples"    -> makeExamplesAnnotation node    |> Some
  | _             -> None

let makeIfCondition node =
  Result.map Condition.If (parse node)

let makeElseCondition node =
  Result.map Condition.Else (parse node)

let makeThenCondition node =
  Result.map Condition.Then (parse node)

let foldListResult f xs =
  List.fold (fun acc x ->
    Result.bind (fun y ->
      Result.map (fun u -> u :: y) (f x)) acc
  ) (Ok List.empty) xs

let makeAllOfCondition = function
| JsonNode.Array xs ->
  // TODO length > 0
  Result.map Condition.AllOf (foldListResult parse xs)
| _ -> Error "Invalid allOf"

let makeAnyOfCondition = function
| JsonNode.Array xs ->
  // TODO length > 0
  Result.map Condition.AnyOf (foldListResult parse xs)
| _ -> Error "Invalid anyOf"

let makeOneOfCondition = function
| JsonNode.Array xs ->
  // TODO length > 0
  Result.map Condition.AnyOf (foldListResult parse xs)
| _ -> Error "Invalid oneOf"

let makeNotCondition node =
  Result.map Condition.Not (parse node)

let makeCondition name node =
  match name with
  | "if"    -> makeIfCondition node    |> Some
  | "then"  -> makeThenCondition node  |> Some
  | "else"  -> makeElseCondition node  |> Some
  | "allOf" -> makeAllOfCondition node |> Some
  | "anyOf" -> makeAnyOfCondition node |> Some
  | "oneOf" -> makeOneOfCondition node |> Some
  | "not"   -> makeNotCondition node   |> Some
  | _       -> None

let obj m =
  let init =
    {
      Annotations = List.empty;
      Assertions  = List.empty;
      Conditions  = List.empty;
    }
  let init = init |> ObjectSchema |> Ok

  let folder s k v =
    let res = makeConstraint k v
    match res, s with
    | _, Error e        -> Error e
    | None, s           -> s
    | Some (Error e), _ -> Error e
    | Some (Ok x), Ok (ObjectSchema schema) ->
      let xs = x :: schema.Assertions
      { schema with Assertions = xs } |> ObjectSchema |> Ok
    // Should never be calling this with TrueSchema / FalseSchema
    | _                 -> Error "invalid state"

  Map.fold folder init m

let parse = function
| JsonNode.Object m      -> obj m
| JsonNode.Boolean true  -> Ok TrueSchema
| JsonNode.Boolean false -> Ok FalseSchema
| _                      -> Error "Root level items must be objects or booleans"
