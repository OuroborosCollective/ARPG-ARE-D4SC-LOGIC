# Fadenwerk JSON verification

This console regression compiles the real linked Editor/FwJson.cs source without Unity dependencies. It parses its output with System.Text.Json and checks all C0 controls, quotes, backslashes, HTML delimiters and Unicode U+2028/U+2029. The original owner candidate failed compilation with CS1010/CS1011 at the raw line-separator character literals. Explicit C# Unicode escapes preserve the intended codepoints.

Run: dotnet run --project Tests/FadenwerkJsonVerification/FadenwerkJsonVerification.csproj --configuration Release

This is a serialization regression; it does not prove Unity Editor import, game quest transactions, player persistence, HTTP authorization, or Android behavior.
