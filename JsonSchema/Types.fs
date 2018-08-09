module rec JsonSchema.Types

open JsonDeserializer.Types
open System.Text.RegularExpressions
open System.Collections.Generic

// TODO:
// - ECMA262 regex validation
// - Numeric precision?

type JsonSchema =
  | TrueSchema
  | FalseSchema
  | ObjectSchema of Assertion list * Annotation list

[<RequireQualifiedAccess>]
type ScalarType =
  | Null
  | Boolean
  | Object
  | Array
  | Number
  | String
  | Integer

type TypeAssertion =
  | ScalarType of ScalarType
  | ListType of ScalarType list

type Annotation = Annotation

[<RequireQualifiedAccess>]
type SchemaNumber =
  | Integer of int64
  | Double of double

type ItemsAssertion =
  | SingletonItemSchema of JsonSchema
  | MultiJsonSchema of JsonSchema list

type Dependency =
  | SchemaDependency of JsonSchema
  | ArrayDependency of string list

[<RequireQualifiedAccess>]
type Assertion =
  // Validation keywords for any instance type
  | Type                 of TypeAssertion
  | Enum                 of JsonNode list
  | Const                of JsonNode
  // Validation keywords for numeric instances
  | MultipleOf           of SchemaNumber
  | Maximum              of SchemaNumber
  | ExclusiveMaximum     of SchemaNumber
  | Minimum              of SchemaNumber
  | ExclusiveMinimum     of SchemaNumber
  // Validation keywords for strings
  | MaxLength            of uint64
  | MinLength            of uint64
  | Pattern              of Regex
  // Validation keywords for arrays
  | Items                of ItemsAssertion
  | AdditionalItems      of JsonSchema
  | MaxItems             of uint64
  | MinItems             of uint64
  | UniqueItems          of bool
  | Contains             of JsonSchema
  // Validation keywords for objects
  | MaxProperties        of uint64
  | MinProperties        of uint64
  | Required             of string list
  | Properties           of Map<string, JsonSchema>
  | PatternProperties    of Dictionary<Regex, JsonSchema>
  | AdditionalProperties of JsonSchema
  | Depenedenciesof      of Map<string, Dependency>
  | PropertyNames        of JsonSchema
