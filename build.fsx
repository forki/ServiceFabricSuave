// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
#r "System.Management.Automation"
#r "System.Core.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open Fake.ZipHelper
open System
open System.IO
open System.Management.Automation
open System.Management.Automation.Runspaces

let applicationName = "fabric:/CalculatorService"
let applicationType = "CalculatorServiceType"
let applicationVersion = "1.0.0.0" //TODO: THIS SHOULD BE HANDLED SMARTER

let pkgDir = "temp" </> "pkg"
let hostPkgDir = "src" </> "FabricHost" </> "pkg" </> "Release"
let buildDir = "temp" </> "build"

Target "Clean" (fun _ ->
    CleanDirs [buildDir; pkgDir]
)

Target "Package" (fun _ ->
    !! "src/*/*.sfproj"
    |> MSBuildRelease buildDir "Package"
    |> ignore

    CopyDir pkgDir hostPkgDir  (fun _ -> true)
)

//PowerShell Helpers

let invoke (modules : string []) (ps : PowerShell) =
    let initial = InitialSessionState.CreateDefault ()
    initial.ImportPSModule modules
    let runspace = RunspaceFactory.CreateRunspace initial
    runspace.Open ()
    ps.Runspace <- runspace
    ps.Invoke() |> Seq.iter (printfn "%A")
    runspace

let invokeWithRunspace runspace (ps : PowerShell) =
    ps.Runspace <- runspace
    ps.Invoke() |> Seq.iter (printfn "%A")
    runspace

let psCommand (command : string) =
    PowerShell.Create().AddCommand command

let psAddParameter (n,v) (ps : PowerShell) =
    ps.AddParameter(n,v)

//Service Fabric Helpers

let modules = [| @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/PSModule/ServiceFabricSDK/ServiceFabricSDK.psm1" |]
let localRunspace =
    "Connect-ServiceFabricCluster"
    |> psCommand
    |> psAddParameter ("ConnectionEndpoint", "localhost:19000")
    |> invoke modules

let remove runspace =
    try
        "Remove-ServiceFabricApplication"
        |> psCommand
        |> psAddParameter ("ApplicationName", applicationName)
        |> psAddParameter ("Force", SwitchParameter.Present)
        |> invokeWithRunspace runspace
        |> ignore
    with
    | ex -> traceImportant ex.Message
    try
        "Unregister-ServiceFabricApplicationType"
        |> psCommand
        |> psAddParameter ("ApplicationTypeName", applicationType)
        |> psAddParameter ("ApplicationTypeVersion", applicationVersion)
        |> psAddParameter ("Force", SwitchParameter.Present)
        |> invokeWithRunspace runspace
        |> ignore
    with
    | ex -> traceImportant ex.Message

let deploy runspace =
    "Publish-NewServiceFabricApplication"
        |> psCommand
        |> psAddParameter ("ApplicationPackagePath", pkgDir)
        |> psAddParameter ("ApplicationName", applicationName)
        |> invokeWithRunspace runspace
        |> ignore

//Local Deployment

Target "StartLocalCluster" (fun _ ->
    let modules = [| @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/Scripts/ClusterSetupUtilities.psm1"
                     @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/Scripts/DefaultLocalClusterSetup.psm1"
                  |]
    "Set-LocalClusterReady"
    |> psCommand
    |> invoke modules
    |> ignore
)

Target "RemoveFromLocal" (fun _ -> remove localRunspace)

Target "DeployLocal" (fun _ -> deploy localRunspace)

Target "Default" DoNothing

"Clean"
  ==> "Package"

"RemoveFromLocal"
  ==> "DeployLocal"

"Package"
  ==> "DeployLocal"
  ==> "Default"


RunTargetOrDefault "Default"
