<div id="top"></div>

<!-- TITLE -->
# Azure Multi-Tenant User Synchronization
Azure Function App that synchronizes AD users between two tenants.

<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#prerequisites">Prerequisites</a></li>
        <ul>
          <li><a href="#runninglocally">Running Locally</a></li>
          <li><a href="#runningonazure">Running on Azure</a></li>
        </ul>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#acknowledgments">Acknowledgments</a></li>
  </ol>
</details>

<!-- ABOUT THE PROJECT -->
## About The Project

![](images/userssync.drawio.png)

The repository contains an Azure Serveless Function that synchronizes AD users between two tenants.

Motivation:
* The need to sync users between two tenants.
* Currently, an open source solution that solves the multi-tenant user synchronization problem is inexistent.
* It's a prerequesite to implement an Azure Entreprise-Scale Landing Zone.

<p align="right">(<a href="#top">back to top</a>)</p>

### Built With

The main frameworks used to develop the synchronization app are described below:

* [Azure Serveless Functions](https://azure.microsoft.com/en-us/services/functions/)
* [dotnet core 3.1](https://dotnet.microsoft.com/)

<p align="right">(<a href="#top">back to top</a>)</p>

<!-- GETTING STARTED -->
## Getting Started

Below you will find the installation process for running the function locally or on an Azure tenant.

### Prerequisites

#### Running Locally

1. Install the [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=v3%2Clinux%2Ccsharp%2Cportal%2Cbash%2Ckeda#v2)
2. Configure an [Azure Storage Account Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio)
3. Configure the local.settings.json file on the root folder
    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "<LOCAL_STORAGE_CONNECTION_STRING>",
        "ScheduleTrigger": "0 */5 * * * *",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
        "SRC_TENANT_ID": "<THE_SOURCE_TENANT_ID>",
        "SRC_GROUP_ID": "<THE_SOURCE_GROUP_ID>",
        "DST_TENANT_ID": "<THE_DESTINATION_TENANT_ID>",
        "DST_GROUP_ID": "<THE_DESTINATION_GROUP_ID>",
        "CLIENT_ID": "<THE_APP_REGISTRATION_ID>",
        "CLIENT_SECRET": "<THE_APP_REGISTRATION_SECRET>",
        "REQUEST_MAX_RETRIES": "5",
        "INVITE_BASE_URL": "https://portal.azure.com"
      }
    }
    ```
4. Start the function
   ```bash
   func start
   ```

#### Running on Azure
<p align="right">(<a href="#top">back to top</a>)</p>