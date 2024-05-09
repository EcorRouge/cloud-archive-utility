# OneDrive connector
## Description
Onedrive connector Uses  [Daemon application authontication flow] {:target="_blank" rel="noopener"} on which the application can acquire a token to call a web API on behalf of the user/s
in our case OneDrive Connector will aquire a token on behalf of Azure Active directory users, and will go through all users and for each user will go throguh all the drives he has in case he has more than one drive 

## How to get keys (ClientId,ClientSecret,TenantId)

### For Developers
1. You have to get [Microsoft 365 Developer] {:target="_blank" rel="noopener"}Program
2. Enable Sample data packs for (Users, Mail&Events, SharePoint) this will create sample of 26 users and will create demo emails and events for them and Sharepoint and OneDrive drives as well.
3. Create Azure free trial account
4. go to [Azure Portal] {:target="_blank" rel="noopener"}> Azure Active Directory > App registrations
5. Register new app from "+New Registration" [Register damon app instructions] {:target="_blank" rel="noopener"}

here you will get the ClientId, ClientSecret and TenantId
![App keys](https://i.ibb.co/F0vmKM9/Screenshot-2021-08-17-220634.png "App keys")

## Permissions
To grant the application the permission to list AD Users and OneDrives two Permissions are required

1. User.Read.All 
2. Files.Read.All  

Files deletion requires Files.ReadWrite.All permission.

**How to give application required permissions?**

- Login to https://portal.azure.com/ {:target="_blank" rel="noopener"}
- go to Azure Active Directory > App registrations 
- select your application 
- form the left menu select "API permissions"
- Click "Add Permissions"
- "Microsoft APIs" tab > select Microsoft Graph >
- Select "Application Permission" 
  1. Search for User.Read.All and select it
  2. Search for Files.Read.All and select it 
- close the window and click Grant Admin consent for [your application name]
- click yes on popup message 

## OneDrive Connector Commands

	  type: 'DISCOVER',
        data: [{
          key: 'provider',
          value: 'onedrive'
        },
		{key: 'connection',
          value: 'clientId=dabfabc4-4d42-4479-b818-566a65a493f0,clientSecret=x.~V1Kb8ZH.3~0rca.wsNj4~BLj5Xv5j7a,tenant=ce76a08d-f722-438a-8315-3b82ee416a7d'
        }]

**Command includes specific users Emails**

```
type: 'DISCOVER',
data: [{key: 'provider',value: 'onedrive'},
{key: 'connection',
value: 'clientId=dabfabc4-4d42-4479-b818-566a65a493f0,clientSecret=x.~V1Kb8ZH.3~0rca.wsNj4~BLj5Xv5j7a,tenant=ce76a08d-f722-438a-8315-3b82ee416a7d,usersIdList=GradyA@alsaqqa.onmicrosoft.com|AlexW@alsaqqa.onmicrosoft.com'}]

```

Connection value contains : 
- clientId :  Application ( Client ) ID
- clientSecret :  is application Client Credintial (Secret)
- tenant : Directory (Tenant) ID

## How to specify Users Id's

 **1- Got to [Windows Azure Portal] {:target="_blank" rel="noopener"}**

 **2- Select "Azure Active Directory"**
<kbd>![Azure Active Directory](https://i.imgur.com/QYrdWiE.png "Azure Active Directory")</kbd>
 **3- From Manage select Users**
<kbd>![Users](https://i.imgur.com/yvX7fs1.png "Users")</kbd>

 **4- All Users (Preview ) then select a user**
<kbd>![Users](https://i.imgur.com/MnYx8Ji.png  "Users")</kbd>

 **5- Specify User Id as of the following image**
<kbd>![Users](https://i.imgur.com/xh8umi5.png  "Users")</kbd>

**Note** : You can get list of users throguh Microsft Graph API form the endpoint 
```
https://docs.microsoft.com/en-us/graph/api/user-list?view=graph-rest-1.0&tabs=http 
```

 we have already implemented a method to call List of Users end point you can find [here]{:target="_blank" rel="noopener"}


### Scan Method File Parameter Fromat
DownloadResourceAsync method parameter format  ``` {driveId}:{ItemId}:{fileSize}:{shortFileName} ```

example:

``` b!9-j6SsIaUk6DTo9VEzzr_Vm3zMmFyC1Mm3k_tkcvxRElO7E6hhmxRaIJl2SiZ2jV:01MIXXXWBXWMSWB2QDHFBKO5BO5BPU5Z4B:17147:pic1.jpg ```

[Daemon application authontication flow]: <https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-daemon-overview>
[Microsoft 365 Developer]: <https://developer.microsoft.com/en-us/microsoft-365/profile>
[Azure Portal]: <https://portal.azure.com/>
[Register damon app instructions]: <https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-v2-netcore-daemon>
[Windows Azure Portal]: <https://portal.azure.com/>
[here]: <https://github.com/EcorRouge/reveles-docker-collector/blob/f8f7a73e048484482b13c430d3889242575453c8/Reveles.Collector.Cloud.Connector/MicrosoftGraphApi/MicrosoftGraphAPIClient.cs#L75>

