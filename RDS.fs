// #r @"/Users/krzkik/.nuget/packages/awssdk.core/3.5.1.32/lib/netcoreapp3.1/AWSSDK.Core.dll";;
// #r @"/Users/krzkik/.nuget/packages/awssdk.cloudwatch/3.5.0.34/lib/netcoreapp3.1/AWSSDK.CloudWatch.dll";;
// #r @"/Users/krzkik/.nuget/packages/awssdk.rds/3.5.4.7/lib/netcoreapp3.1/AWSSDK.RDS.dll";;

open System
open System.IO
open System.Collections.Generic

open Amazon
open Amazon.CloudWatch
open Amazon.CloudWatch.Model

open Amazon.RDS
open Amazon.RDS.Model

type Period = 
  struct 
    val HOUR: int
    val DAY: int 
    new (_) = {HOUR = 60*60; DAY = 60*60*24}
    member this.Hours(n:int) = this.HOUR * n
  end

let getDBInstances (region:RegionEndpoint) =
  let rdsClient = new AmazonRDSClient(region) in
  let instances = rdsClient.DescribeDBInstancesAsync() in
  instances.Result.DBInstances

let filterAvailable (l:List<DBInstance>) = l |> Seq.filter (fun i -> i.DBInstanceStatus = "available")
let filterStopped (l:List<DBInstance>) = l |> Seq.filter (fun i -> i.DBInstanceStatus = "stopped")

let getRDSCPUMetrics (region:RegionEndpoint) period days dbid = 
  let cwClient = new AmazonCloudWatchClient(region) in
  let cwRequest = GetMetricStatisticsRequest(Period = period
    , MetricName = "CPUUtilization"
    , StartTimeUtc = DateTime.UtcNow.AddDays(double -days)
    , EndTimeUtc = DateTime.UtcNow
    , Statistics = new List<string>(["Maximum"; "Average"])
    , Dimensions = new List<Dimension>([Dimension(Name = "DBInstanceIdentifier", Value = dbid)])
    , Namespace = "AWS/RDS") in
  let metrics = cwClient.GetMetricStatisticsAsync(cwRequest) in
  metrics.Result.Datapoints
  
[<EntryPoint>]
let main argv =
  printfn "(c) 2020 Syncron AWS Cost Reporting"

  let periods = Period(None)

  let usRDSInstances = 
    getDBInstances RegionEndpoint.USEast1 
    |> filterAvailable
  
  for inst in usRDSInstances do
    printfn "%-32s %-24s %-16s %-16s %10d %10d" 
      inst.DBClusterIdentifier 
      inst.Engine
      inst.DBInstanceClass
      inst.DBInstanceStatus
      inst.AllocatedStorage
      inst.BackupRetentionPeriod

  let lmetric = getRDSCPUMetrics RegionEndpoint.USEast1 (periods.Hours 4) 3 "db01"
  for met in lmetric do
    printfn "%s %6.2f %6.2f" 
      ((met.Timestamp).ToString())
      met.Maximum 
      met.Average

  // for inst in usRDSInstances do
  //   for m in getRDSCPUMetrics RegionEndpoint.USEast1 (periods.Hours 4) 3 inst.DBInstanceIdentifier do
  //     printfn "%A %A" inst.DBInstanceIdentifier m.Average

  0 // return an integer exit code
