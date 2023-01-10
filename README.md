# AzureImageProcessor
Can be tested from [here](https://imageprocessor-wa.azurewebsites.net). 

## Introduction. 
This application allows users to easily generate all of the required app store screenshots for their iOS app, without the need for access to multiple devices. Simply upload a single high-resolution screenshot, and our service will automatically generate all of the different sizes and resolutions needed for the App Store. This includes support for all of the latest iPhone and iPad models, ensuring that your app looks great on all devices. Our easy-to-use interface makes it quick and simple to get all of your screenshots created and uploaded to the App Store, saving you time and hassle on photo editing programs.  
 
 
 ![Schema](https://i.imgur.com/bxSGgzG.png)
 
## Azure Service Used 

### Azure Container Registry
Host my web application docker container image

### App Service
Host the web application(.net mvc project) container image, use the image as web portal for application. 

### Storage Account
Store the users uploaded images also as the processed images, i have also used queues to store blobs names to be used by a cleaning app function that runs every hour

### Azure Functions
Run a timer trigger function that runs every hour to clean the storage account container, the life cycle management policy allow clean storage for blobs with  min age of one day, same thing as Task (automation) which is just a logic app . those solution will not work for my case since storage size can evole drasticly in few hours if application is being used by many users.  

Example:  
a bmp image size can go up to 10mb , this application generates 10 images per user request, so for every user request 100MB of storage is filled.  
For 100 users per day this could go easly to 10 gb of storage which will certainly bring cost higher, 
the solution i have found is to create my own cleaning mecanism wich consit on the following:
when user send a processing request, i collect the generated blobs names and send them to a storage queue
then i have a cleaning timer trigger function app that runs every hour, it checks for messages in the queue , if it finds any it check the insertion date to calculate the age of the message , if the age is more than one hour then i process the queue message by extracting the blob names associated and proceed with the deletion of the blobs. 
