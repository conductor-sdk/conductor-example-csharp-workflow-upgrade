
using Conductor.Api;
using Conductor.Client;
using Conductor.Client.Models;
using static Conductor.Client.Models.TaskResult;
using Conductor.Client.Authentication;

class WorkflowMigration
{

    private static Configuration configuration = new Configuration()
    {
        BasePath = "conductor_url",
        AuthenticationSettings = new OrkesAuthenticationSettings("keyId", "keySecret")
    };

    public static void Main(string[] args)
    {

        string workflowId = args[0];
        string workflowName = args[1];
        int workflowVersion = args[2] != null ? int.Parse(args[2]) : 0;

        // This is the map of new task ref names to expected output.
        var dictionary = args[3].Select(a => a.ToString().Split('='))
                     .ToDictionary(a => a[0], a => a.Length == 2 ? a[1] : null);

        WorkflowResourceApi workflowResourceApi = new WorkflowResourceApi(configuration);
        TaskResourceApi taskResourceApi = new TaskResourceApi(configuration);

        Dictionary<string, Conductor.Client.Models.Task> oldWorkflowTaskMapping = new Dictionary<string, Conductor.Client.Models.Task>();
        HashSet<string> taskNames = new HashSet<string>();

        Workflow workflow = workflowResourceApi.GetExecutionStatus(workflowId, true);
        workflow.Tasks.ForEach(task =>
        {
            if (task.TaskType.Equals(task.ReferenceTaskName) && task.Status.Equals(Conductor.Client.Models.Task.StatusEnum.COMPLETED))
            {
                taskNames.Add(task.ReferenceTaskName);
                oldWorkflowTaskMapping[task.ReferenceTaskName] = task;
            }
        });

        StartWorkflowRequest startWorkflowRequest = new StartWorkflowRequest(name: workflowName, version: workflowVersion == 0 ? null : workflowVersion);

        Dictionary<string, object> input = new Dictionary<string, object>();
        input["_rerunFromWorkflowId"] = workflowId;

        startWorkflowRequest.Input = input;
        Dictionary<string, string> domain = new Dictionary<string, string>();
        taskNames.ToList().ForEach(taskName =>
        {
            domain[taskName] = System.Guid.NewGuid().ToString();
        });
        startWorkflowRequest.TaskToDomain = domain;
        string newWorkflowId = workflowResourceApi.StartWorkflow(startWorkflowRequest);

        workflowResourceApi.Terminate(workflowId, "Terminated because of migration. A new workflow " + newWorkflowId +" has been started");

        Workflow newWorkflow = workflowResourceApi.GetExecutionStatus(newWorkflowId, true);

        while(!newWorkflow.Status.Equals(WorkflowStatus.StatusEnum.COMPLETED))
        {
            newWorkflow = workflowResourceApi.GetExecutionStatus(newWorkflowId, true);
            List<Conductor.Client.Models.Task> inProgressTask = newWorkflow.Tasks.FindAll(task =>
                                                                                        task.Status.Equals(Conductor.Client.Models.Task.StatusEnum.INPROGRESS) ||
                                                                                        task.Status.Equals(Conductor.Client.Models.Task.StatusEnum.SCHEDULED));
            inProgressTask.ForEach(task =>
            {
                if (oldWorkflowTaskMapping.ContainsKey(task.ReferenceTaskName))
                {
                    TaskResult taskResult = new TaskResult(taskId: task.TaskId, workflowInstanceId: task.WorkflowInstanceId);
                    taskResult.Status = StatusEnum.COMPLETED;
                    taskResult.OutputData = oldWorkflowTaskMapping[task.ReferenceTaskName].OutputData;
                    taskResult.TaskId = task.TaskId;
                    taskResourceApi.UpdateTask(taskResult);
                    Console.WriteLine("Task " + task.ReferenceTaskName + " from the workflowId " + newWorkflowId + " has been marked Completed ");
                } else if (dictionary.ContainsKey(task.ReferenceTaskName))
                {
                    TaskResult taskResult = new TaskResult(taskId: task.TaskId, workflowInstanceId: task.WorkflowInstanceId);
                    taskResult.Status = StatusEnum.COMPLETED;
                    Dictionary<string, object> output = new Dictionary<string, object>();
                    output["migration_output"] = dictionary[task.ReferenceTaskName] ?? "no_output";
                    taskResult.OutputData = output;
                    taskResult.TaskId = task.TaskId;
                    taskResourceApi.UpdateTask(taskResult);
                    Console.WriteLine("Task " + task.ReferenceTaskName + " from the workflowId " + newWorkflowId + " has been marked Completed ");
                } else
                {
                    throw new Exception("Task " + task.ReferenceTaskName + " output not provided");
                }
            });
            newWorkflow = workflowResourceApi.GetExecutionStatus(newWorkflowId, true);
        }
        Console.WriteLine("Workflow workflowId " + newWorkflowId + " Completed Successfully");
    }
}