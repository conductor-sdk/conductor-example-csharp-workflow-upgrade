# conductor-example-csharp-workflow-upgrade
This example demonstrates the workflow migration example for c#. Whenever the workflows are running and we want all running workflows also to migrate to a new workflow definition from the point of the current execution.
Steps,
1. Change conductor_server url and put keyId and keysSecret accordingly in the file WorkflowMigration.cs
2. Put all the workflow that has to be migrated in the list workflowNames in WorkflowMigration.cs
3. Run the project.
