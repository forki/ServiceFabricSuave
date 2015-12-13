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

Target "StartLocalCluster" (fun _ ->
    let initial = InitialSessionState.CreateDefault ()
    initial.ImportPSModule [| @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/Scripts/ClusterSetupUtilities.psm1"
                              @"C:/Program Files/Microsoft SDKs/Service Fabric/Tools/Scripts/DefaultLocalClusterSetup.psm1"
                           |]
    let runspace = RunspaceFactory.CreateRunspace initial
    runspace.Open ()
    let ps = PowerShell.Create().AddCommand "Set-LocalClusterReady"
    ps.Runspace <- runspace
    try
        ps.Invoke() |> Seq.iter (printfn "%O")
    with
       | _ -> ps.Streams.Error |> Seq.iter (printfn "%O")
)

let (=!>) a b = a, (b |> unbox<obj>)

Target "DeployLocal" (fun _ ->
    let runspace = RunspaceFactory.CreateRunspace ()
    runspace.Open ()
    let pipeline = runspace.CreatePipeline ()

    let ps =
        [ "ApplicationPackagePath" =!> "src\FabricHost\pkg\Debug"
          "PublishProfileFile" =!> "src\FabricHost\PublishProfiles\Local.xml"
          "DeployOnly" =!> false
          "UnregisterUnusedApplicationVersionsAfterUpgrade" =!> false
          "ForceUpgrade" =!> false
          "OverwriteBehavior" =!> "SameAppTypeAndVersion"
          "ErrorAction" =!> "Stop"
        ] |> List.fold (fun (acc : PowerShell) (k,v) -> acc.AddParameter(k,v) ) (PowerShell.Create().AddScript("src\FabricHost\Scripts\Deploy-FabricApplication.ps1"))

    ps.Runspace <- runspace
    try
        ps.Invoke() |> Seq.iter (printfn "%O")
        printfn "OK"
    with
    | ex ->
        traceError "ERROR:\n"
        ps.Streams.Error |> Seq.iter (sprintf "%O\n" >> traceError)
        raise ex
)


Target "Default" DoNothing

"StartLocalCluster"
  ==> "DeployLocal"
  ==> "Default"


RunTargetOrDefault "Default"
