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
let release = LoadReleaseNotes "RELEASE_NOTES.md"

let project = "CalculatorService"
let summary = "Demo of F# Suave based Service Fabric stateless service"


// --------------------------------------------------------------------------------------
// Building and packaging
// --------------------------------------------------------------------------------------

let pkgDir = "temp" </> "pkg"
let hostPkgDir = "src" </> "FabricHost" </> "pkg" </> "Release"
let buildDir = "temp" </> "build"

Target "Clean" (fun _ ->
    CleanDirs [buildDir; pkgDir]
)

Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product project
          Attribute.Description summary
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.fsproj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->  CreateFSharpAssemblyInfo (folderName @@ "AssemblyInfo.fs") attributes)
)

Target "Package" (fun _ ->
    !! "src/*/*.sfproj"
    |> MSBuildRelease buildDir "Package"
    |> ignore

    CopyDir pkgDir hostPkgDir  (fun _ -> true)
)

// --------------------------------------------------------------------------------------
// PowerShell Helpers
// --------------------------------------------------------------------------------------

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

// --------------------------------------------------------------------------------------
// Service Fabric Helpers
// --------------------------------------------------------------------------------------

let modules = [| @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/PSModule/ServiceFabricSDK/ServiceFabricSDK.psm1" |]

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

// --------------------------------------------------------------------------------------
// Local Deployment
// --------------------------------------------------------------------------------------

let localRunspace =
    "Connect-ServiceFabricCluster"
    |> psCommand
    |> psAddParameter ("ConnectionEndpoint", "localhost:19000")
    |> invoke modules

Target "StartLocalCluster" (fun _ ->
    let modules =
        [|  @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/Scripts/ClusterSetupUtilities.psm1"
            @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/Scripts/DefaultLocalClusterSetup.psm1" |]
    "Set-LocalClusterReady"
    |> psCommand
    |> invoke modules
    |> ignore
)

Target "RemoveFromLocal" (fun _ -> remove localRunspace)

Target "DeployLocal" (fun _ -> deploy localRunspace)

// --------------------------------------------------------------------------------------
// General FAKE stuff
// --------------------------------------------------------------------------------------

Target "Default" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Package"

"RemoveFromLocal"
  ==> "DeployLocal"

"Package"
  ==> "DeployLocal"
  ==> "Default"


RunTargetOrDefault "Default"
