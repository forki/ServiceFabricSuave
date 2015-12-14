open Suave
open Suave.Http.Successful
open Suave.Types
open Suave.Web
open System.Fabric
open System.Threading
open System.Threading.Tasks
open Suave.Http
open Microsoft.ServiceFabric.Services.Runtime
open Microsoft.ServiceFabric.Services.Communication.Runtime

open Services


[<EntryPoint>]
let main argv =
    use fabricRuntime = FabricRuntime.Create()
    fabricRuntime.RegisterServiceType("CalculatorServiceType", typeof<CalculatorService>)
    Thread.Sleep Timeout.Infinite
    0