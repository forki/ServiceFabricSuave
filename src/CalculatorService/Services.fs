module Services

open ServicesCommon
open Suave
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Successful
open Suave.Web

//Plain logic, no dependencies on Suave and Service Fabric - easy to unit test
module Logic = 
    let add (a,b) = (a + b).ToString()
    let subtract (a,b) = (a - b).ToString() 

let calculatorService = 
    choose [
        pathScan "/%d+%d" (Logic.add >> OK)
        pathScan "/%d-%d" (Logic.subtract >> OK)
        RequestErrors.NOT_FOUND "Found no handlers"
    ]

type CalculatorService () = inherit SuaveService(calculatorService)
