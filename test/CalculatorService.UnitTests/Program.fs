open Fuchu
open Fuchu.FuchuFsCheck
open Services.Logic


let logicTests = 
    testList "Calulator Service Logic Test" [
        testProperty "Addition" (fun a b -> add(a,b) = (a + b).ToString())
        testProperty "Subtraction" (fun a b -> subtract(a,b) = (a - b).ToString())
    ]


[<EntryPoint>]
let main argv = 
    runParallel logicTests