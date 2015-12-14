namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("CalculatorService.IntegrationTests")>]
[<assembly: AssemblyProductAttribute("CalculatorService")>]
[<assembly: AssemblyDescriptionAttribute("Demo of F# Suave based Service Fabric stateless service")>]
[<assembly: AssemblyVersionAttribute("1.0.1")>]
[<assembly: AssemblyFileVersionAttribute("1.0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.1"
