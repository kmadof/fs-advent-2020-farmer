open Farmer
open Farmer.Builders
open System.Collections.Generic
open Farmer.Sql

[<EntryPoint>]
let main argv =

    let isDryRun : bool = 
        match argv.[0] with
        | "true" | "True" -> true
        | "false" | "False" -> false
        | _-> failwith("Error: returns " + argv.[0])
    
    let project = argv.[1]
    let env = argv.[2]
    let sqlPassword = argv.[3]

    //gran campo nevado
    let prefix = $"{project}-{env}"
    let rgName = $"{prefix}-rg"
    let frontAppName = $"{prefix}-front-app"
    let backAppName = $"{prefix}-back-app"
    let sqlServerName = $"{prefix}-sql-server"

    let createServicePlans env =
        let servicePlans = Dictionary<_,_>()
        match env with
        | "dev" -> 
            let plan = servicePlan {
                name $"{prefix}-appplan"
                sku WebApp.Sku.F1
            }
            servicePlans.Add(frontAppName, plan)
            servicePlans.Add(backAppName, plan)
        | "staging" ->
            let frontPlan = servicePlan {
                name $"{prefix}-fornt-appplan"
                sku WebApp.Sku.B1
            }
            servicePlans.Add(frontAppName, frontPlan)
            let backPlan = servicePlan {
                name $"{prefix}-back-appplan"
                sku WebApp.Sku.B1
            }
            servicePlans.Add(backAppName, backPlan)
        | _-> failwith("Error: returns " + argv.[0])

        servicePlans

    let servicePlans = createServicePlans env

    let ai = appInsights {
        name $"{prefix}-appInisght"
    }

    let frontApp = webApp {
        name frontAppName
        link_to_service_plan servicePlans.[frontAppName]
        link_to_app_insights ai
    }

    let backApp = webApp {
        name backAppName
        link_to_service_plan servicePlans.[backAppName]
        link_to_app_insights ai
    }

    let uniquePlans = servicePlans.Values 
                        |> Seq.distinct 
                        |> Seq.map (fun plan -> plan :> IBuilder)
                        |> Seq.toList

    let databases = sqlServer {
        name sqlServerName
        admin_username "admin_username"
        enable_azure_firewall

        elastic_pool_name "mypool"
        elastic_pool_sku PoolSku.Basic100

        add_databases [
            sqlDb { name $"{prefix}-sql"; sku Basic }
        ]
    }

    let deployment = arm {
        location Location.WestEurope
        add_resources uniquePlans
        add_resource ai
        add_resource frontApp
        add_resource backApp
        add_resource databases
    }

    let executeDeployment = 
        match isDryRun with
        | true ->  Writer.quickWrite  "myFirstTemplate"
        | false -> 
            let deploy deployment =

                printf "Deploying resources into %s using Farmer\n" rgName

                Deploy.execute rgName [$"password-for-{sqlServerName}", sqlPassword ] deployment
                |> ignore
                printf "Deployment of resources into %s complete!\n" rgName

            deploy

    deployment
    |> executeDeployment

    0 // return an integer exit code