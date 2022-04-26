# Duo-ECMA2

Duo management agent for Microsoft Identity Manager (MIM) 2016

## This repo
This repo contains a .NET Visual Studio project that will allow Microsoft Identity Manager (MIM) 2016 to connect to your Duo tenant and manage users.

## Prerequisites
* Create the AdminAPI application by following the directions in this link: https://duo.com/docs/adminapi
* Note your API hostname from the application. Ex: https://api-XXXXXXXX.duosecurity.com
* Write down your integration key and secret key.

## Installation
1. Compile the project and locate the files within the ~/bin/Debug folder of the project.
2. Copy the files to your "Extensions" folder. The default path should be: C:\Program Files\Microsoft Forefront Identity Manager\2010\Synchronization Service\Extensions
3. Create a new management agent within MIM and select Extensible Connectivity 2.0 from the dropdown. Name the agent and give it a good description.
4. Under the "Select Extension DLL" menu, select the DuoCSExtension.dll from the "browse" menu and click on "Refresh interfaces".
5. Fill in the form fields from the prerequisites under the "Connectivity" menu.
6. Select the object types MIM will synchronize data with from the "Select Object Types" menu.
7. Select the attributes that your organization requires. Please create an issue if you require custom attributes.
8. Configure the anchor on the next page.
9. Configure the rest of the management agent as necessary.
