# conductor-example-csharp-workflow-upgrade
This example demonstrates the workflow migration example for c#. Whenever the workflows are running and we want the running workflows to migrate to a new workflow definition from the point of the current execution.
Steps,
1. Change conductor_server url and put keyId and keysSecret accordingly in the file WorkflowMigration.cs and build the project.
2. Run the program with command line argument in order as

   a. workflowId <- WorkflowId to migrate.

   b. WorkflowName <- Workflow to run as a replacement for the above workflow.

   c. Workflowversion <- Workflow version for the above workflow.

   d. Map of task_ref_name to output. <- Map of task_Ref_name which were newly added and might not present in the workflow old definition.
 
The example can be extended as per user need.
