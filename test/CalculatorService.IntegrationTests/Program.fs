open Fuchu
open HttpClient

let tests = 
    testList "Calendar Service integration tests" [
        testCase "Addition" (fun _ ->
            let a = 123
            let b = 321
            let res = 
                createRequest Get (sprintf "http://localhost:8505/%d+%d" a b)
                |> getResponseBody
            Assert.Equal("123 + 321", "444", res)
        )

        testCase "Subtraction" (fun _ ->
            let a = 123
            let b = 321
            let res = 
                createRequest Get (sprintf "http://localhost:8505/%d-%d" a b)
                |> getResponseBody
            Assert.Equal("123 - 321", "-198", res)
        )
    ]


[<EntryPoint>]
let main argv = 
    runParallel tests