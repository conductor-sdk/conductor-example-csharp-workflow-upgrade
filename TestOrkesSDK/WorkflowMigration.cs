
using Conductor.Api;
using Conductor.Client;
using Conductor.Client.Authentication;
using Conductor.Client.Extensions;
using Conductor.Client.Interfaces;
using Conductor.Client.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Conductor.Client.Models;
using TestOrkesSDK;
using static Conductor.Client.Models.TaskResult;


class ConsumerExample
{
    static Dictionary<string, Conductor.Client.Models.Task>? oldWorkflowTaskMapping;

    internal class SimpleWorkerNew : IWorkflowTask
    {
        public string TaskType { get; }
        public WorkflowTaskExecutorConfiguration WorkerSettings { get; }

        public SimpleWorkerNew(string taskType = "test-sdk-csharp-task")
        {
            TaskType = taskType;
            WorkerSettings = new WorkflowTaskExecutorConfiguration();
            WorkerSettings.Domain = "after_migration";
        }

        public TaskResult Execute(Conductor.Client.Models.Task task)
        {
            Conductor.Client.Models.Task oldTask = oldWorkflowTaskMapping[task.WorkflowInstanceId + ":" + task.ReferenceTaskName];
            TaskResult taskResult = new TaskResult(taskId:task.TaskId, workflowInstanceId: task.WorkflowInstanceId);
            taskResult.Status = StatusEnum.COMPLETED;
            taskResult.OutputData = oldTask.OutputData;
            taskResult.TaskId = task.TaskId;
            return taskResult;
        }
    }

    private static Configuration configuration = new Configuration()
    {
        BasePath = "conductor_url",
        AuthenticationSettings = new OrkesAuthenticationSettings("keyId", "keySecret")
    };

    public static void Main(string[] args)
    {

        /*
         * 1. Search all the running workflows for the given the type.
         * 2. Terminate all the workflows and store the workflow execution information.
         * 3. Trigger new workflows where the workflow input contains the old workflowId. Domain must be set for all old taskref names to old.
         * 4. Create a map of new workflow run to old workflow run. and poll for the task. When the task comes, find the workflowId. From the map find the old workflowId and using the task refname set the task output.
         */

        List<string> workflowNames = new List<string>
            {
            "csharp"
            };
        WorkflowResourceApi workflowResourceApi = new WorkflowResourceApi(configuration);


        ScrollableSearchResultWorkflowSummary scrollableSearchResultWorkflowSummary = workflowResourceApi.Search(query: "workflowType IN (" + workflowNames[0] + ") AND status IN (RUNNING)", skipCache:true);

        Dictionary<string, Workflow> oldWorkflowMapping = new Dictionary<string, Workflow>();
        Dictionary<string, string> workflowsMapping = new Dictionary<string, string>();
        oldWorkflowTaskMapping = new Dictionary<string, Conductor.Client.Models.Task>();
        HashSet<string> workerNames = new HashSet<string>();

        // Create task mapping. So for the new task we just have to get the old workflowId and related task using below map.
        // Trigger new workflow with old one as input.
        scrollableSearchResultWorkflowSummary.Results.ForEach(workflowSummary => {
            StartWorkflowRequest startWorkflowRequest = new StartWorkflowRequest(name:workflowSummary.WorkflowType);

            Dictionary<string, object> input = new Dictionary<string, object>();
            input["_rerunFromWorkflowId"] = workflowSummary.WorkflowId;

            startWorkflowRequest.Input = input;
            Dictionary<string, string> domain = new Dictionary<string, string>();
            domain["*"] = "after_migration";
            startWorkflowRequest.TaskToDomain = domain;
            String newWorkflowId = workflowResourceApi.StartWorkflow(startWorkflowRequest);
            workflowsMapping[newWorkflowId] = workflowSummary.WorkflowId;


            Workflow workflow = workflowResourceApi.GetExecutionStatus(workflowSummary.WorkflowId, true);
            oldWorkflowMapping[workflowSummary.WorkflowId] = workflow;
            workflow.Tasks.ForEach(task =>
            {
                if (task.GetType().Equals(task.ReferenceTaskName))
                {
                    workerNames.Add(task.ReferenceTaskName);
                    oldWorkflowTaskMapping[newWorkflowId + ":" + task.ReferenceTaskName] = task;
                }
            });

            workflowResourceApi.Terminate(workflowSummary.WorkflowId, "Terminated because of migration. A new workflow " + newWorkflowId +" has been started");

        });


        GetWorkerHost(workerNames);
        Console.ReadLine();
    }

    public static TaskResult Execute(Conductor.Client.Models.Task task)
    {
        Conductor.Client.Models.Task oldTask = oldWorkflowTaskMapping[task.WorkflowInstanceId + ":" + task.ReferenceTaskName];
        TaskResult taskResult = new TaskResult(taskId: task.TaskId, workflowInstanceId: task.WorkflowInstanceId);
        taskResult.Status = StatusEnum.COMPLETED;
        taskResult.OutputData = oldTask.OutputData;
        taskResult.TaskId = task.TaskId;
        return taskResult;
    }

    private static object GetWorkerHost(HashSet<string> workerNames)
    {
        List<GenericWorker> workerTasks = new List<GenericWorker>();
        workerNames.ToList().ForEach(workerName =>
        {
            WorkflowTaskExecutorConfiguration workflowTaskExecutorConfiguration = new WorkflowTaskExecutorConfiguration();
            GenericWorker workerTask = new GenericWorker(taskType: workerName, workflowTaskExecutorConfiguration, executeTaskMethod: );
            workerTasks.Add(workerTask);
        });
        return new HostBuilder()
    .ConfigureServices(
        (ctx, services) =>
        {
            services.AddConductorWorker(configuration);
            workerTasks.ForEach(worker =>
            {
                services.AddConductorWorkflowTask(worker);
            });
            services.WithHostedService<WorkerService>();
        }
    ).ConfigureLogging(
        logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddConsole();
        }
    ).Build().RunAsync();
    }
}