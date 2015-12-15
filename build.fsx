// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE.x64/tools/FakeLib.dll"
#r "System.Management.Automation"
#r "System.Core.dll"
#load "paket-files/fsharp/FAKE/modules/Octokit/Octokit.fsx"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open Fake.ZipHelper
open Octokit
open System
open System.IO
open System.Management.Automation
open System.Management.Automation.Runspaces

// --------------------------------------------------------------------------------------
// Set parameters
// --------------------------------------------------------------------------------------

let project = "CalculatorService"
let summary = "Demo of F# Suave based Service Fabric stateless service"

let applicationName = "fabric:/CalculatorService"

let release = LoadReleaseNotes "RELEASE_NOTES.md"
let releaseHistory = File.ReadAllLines "RELEASE_NOTES.md" |> parseAllReleaseNotes

// --------------------------------------------------------------------------------------
// GitHub configuration
// --------------------------------------------------------------------------------------

let gitOwner = "Krzysztof-Cieslak"
let gitHome = "https://github.com/" + gitOwner
let gitName = "ServiceFabricSuave"


// --------------------------------------------------------------------------------------
// Common
// --------------------------------------------------------------------------------------

let pkgDir = "temp" </> "pkg"
let hostPkgDir = "src" </> "FabricHost" </> "pkg" </> "Release"
let buildDir = "temp" </> "build"
let unitTestBuildDir = "temp" </> "unitTest"
let integrationTestBuildDir = "temp" </> "integrationTest"

Target "Clean" (fun _ ->
    CleanDirs ["temp"; integrationTestBuildDir; unitTestBuildDir; buildDir; pkgDir]
)

// --------------------------------------------------------------------------------------
// Building and packaging
// --------------------------------------------------------------------------------------


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

    !! "*/**/*.fsproj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->  CreateFSharpAssemblyInfo (folderName @@ "AssemblyInfo.fs") attributes)
)

Target "SetVersion" (fun _ ->
    !! "src/*/*/ServiceManifest.xml"
    ++ "src/*/ApplicationManifest.xml"
    |> RegexReplaceInFilesWithEncoding "Version=\"([\d\.]*)\"" (sprintf "Version=\"%s\"" release.AssemblyVersion) Text.Encoding.UTF8
)

Target "Package" (fun _ ->
    !! "src/*/*.sfproj"
    |> MSBuildRelease buildDir "Package"
    |> ignore

    CopyDir pkgDir hostPkgDir  (fun _ -> true)
)

// --------------------------------------------------------------------------------------
// Unit Tests
// --------------------------------------------------------------------------------------

Target "BuildUnitTest" (fun _ ->
    !! "test/*/*UnitTests.fsproj"
    |> MSBuildRelease unitTestBuildDir "Rebuild"
    |> ignore
)

Target "RunUnitTest" (fun _ ->
    let errorCode =
        !! "temp/unitTest/*UnitTests.exe"
        |> Seq.map (fun p -> shellExec {defaultParams with Program = p})
        |> Seq.sum
    if errorCode <> 0 then failwith "Error in tests"
)

// --------------------------------------------------------------------------------------
// Integration Tests
// --------------------------------------------------------------------------------------

Target "BuildIntegrationTest" (fun _ ->
    !! "test/*/*IntegrationTests.fsproj"
    |> MSBuildRelease integrationTestBuildDir "Rebuild"
    |> ignore
)

Target "RunIntegrationTest" (fun _ ->
    let errorCode =
        !! "temp/integration/*IntegrationTests.exe"
        |> Seq.map (fun p -> shellExec {defaultParams with Program = p})
        |> Seq.sum
    if errorCode <> 0 then failwith "Error in tests"
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
    try
        ps.Invoke() |> Seq.iter (printfn "%A")
        runspace
    with
    | ex ->
        ps.Streams.Error |> Seq.iter (sprintf "%O" >> traceError)
        traceError ex.Message
        raise ex

let invokeWithRunspace runspace (ps : PowerShell) =
    ps.Runspace <- runspace
    try
        ps.Invoke() |> Seq.iter (printfn "%A")
        runspace
    with
    | ex ->
        ps.Streams.Error |> Seq.iter (sprintf "%O" >> traceError)
        traceError ex.Message
        raise ex

let psCommand (command : string) =
    PowerShell.Create().AddCommand command

let psScript (script : string) =
    PowerShell.Create().AddScript script

let psAddParameter (n,v) (ps : PowerShell) =
    ps.AddParameter(n,v)

// --------------------------------------------------------------------------------------
// Service Fabric Helpers
// --------------------------------------------------------------------------------------

let programFiles = environVar "ProgramFiles"
let modules = [| programFiles + @"/Microsoft SDKs/Service Fabric/Tools/PSModule/ServiceFabricSDK/ServiceFabricSDK.psm1" |]

let remove runspace =
    let version = if releaseHistory.Length > 1 then releaseHistory.Tail.Head.AssemblyVersion else "1.0.0"
    let applicationType = "CalculatorApplicationType" // this can be read from ApplicationManifest.xml
    traceHeader "Remove Application"
    try
        "Remove-ServiceFabricApplication"
        |> psCommand
        |> psAddParameter ("ApplicationName", applicationName)
        |> psAddParameter ("Force", SwitchParameter.Present)
        |> invokeWithRunspace runspace
        |> ignore
    with
    | ex -> ()
    traceHeader "Remove Application Type"
    try
        "Unregister-ServiceFabricApplicationType"
        |> psCommand
        |> psAddParameter ("ApplicationTypeName", applicationType)
        |> psAddParameter ("ApplicationTypeVersion", version)
        |> psAddParameter ("Force", SwitchParameter.Present)
        |> invokeWithRunspace runspace
        |> ignore
    with
    | ex -> ()

let deploy runspace =
    traceHeader "Publish Application"
    "Publish-NewServiceFabricApplication"
        |> psCommand
        |> psAddParameter ("ApplicationPackagePath", pkgDir)
        |> psAddParameter ("ApplicationName", applicationName)
        |> invokeWithRunspace runspace
        |> ignore

let getCluster () =
    "Connect-ServiceFabricCluster"
    |> psCommand
    |> psAddParameter ("ConnectionEndpoint", "localhost:19000")
    |> invoke modules

// --------------------------------------------------------------------------------------
// Local Deployment
// --------------------------------------------------------------------------------------

Target "StartLocalCluster" (fun _ ->
    traceHeader "Start Cluster"
    // programFiles + @"/Microsoft SDKs/Service Fabric/ClusterSetup/DevClusterSetup.ps1"
    // |> psScript
    // |> invoke [||]
    // |> ignore
    let modules =
        [| programFiles + @"/Microsoft SDKs/Service Fabric/Tools/Scripts/ClusterSetupUtilities.psm1"
           programFiles + @"/Microsoft SDKs/Service Fabric/Tools/Scripts/DefaultLocalClusterSetup.psm1" |]
    "Set-LocalClusterReady"
    |> psCommand
    |> invoke modules
    |> ignore
)

Target "RemoveFromLocal" (fun _ ->
    "Connect-ServiceFabricCluster"
    |> psCommand
    |> psAddParameter ("ConnectionEndpoint", "localhost:19000")
    |> invoke modules
    |> remove
)

Target "DeployLocal" (fun _ ->
    "Connect-ServiceFabricCluster"
    |> psCommand
    |> psAddParameter ("ConnectionEndpoint", "localhost:19000")
    |> invoke modules
    |> deploy)

// --------------------------------------------------------------------------------------
// Release version to GitHub
// --------------------------------------------------------------------------------------

Target "ZipRelease" (fun _ ->
    Directory.GetFiles(pkgDir, "*.*", SearchOption.AllDirectories)
    |> Zip pkgDir ("temp" </> (project + ".zip"))
)

Target "Release" (fun _ ->
    let user =
        match getBuildParam "github-user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserInput "Username: "
    let pw =
        match getBuildParam "github-pw" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion

    let file = !! ("./temp" </> "*.zip") |> Seq.head
    // release on github
    createClient user pw
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    |> uploadFile file
    |> releaseDraft
    |> Async.RunSynchronously
)




// --------------------------------------------------------------------------------------
// General FAKE stuff
// --------------------------------------------------------------------------------------

Target "Default" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "BuildUnitTest"
  ==> "RunUnitTest"

"Clean"
  ==> "AssemblyInfo"
  ==> "BuildIntegrationTest"
  ==> "RunIntegrationTest"

"Clean"
  ==> "AssemblyInfo"
  ==> "SetVersion"
  ==> "RunUnitTest"
  ==> "Package"

"StartLocalCluster"
  ==> "RemoveFromLocal"
  ==> "DeployLocal"

"Package"
  ==> "DeployLocal"
  ==> "RunIntegrationTest"
  ==> "ZipRelease"
  ==> "Release"

"RunIntegrationTest"
  ==> "Default"

RunTargetOrDefault "Default"
