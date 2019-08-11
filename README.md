# Blaise_Case_Handler

Blaise Case Handler is a Windows service for moving, copying, and deleting Blaise cases on Blaise databases. The service is triggered by listening for messages on a RabbitMQ queue, see examples below. The service then uses the Blaise provided .NET API to connect to the Blaise database and perform the necessary action.

# Setup Development Environment

Clone the git respository to your IDE of choice. Visual Studio 2019 is recomended.

Populate the key values in the App.config file accordingly. **Never committ App.config with key values.**

Build the solution to obtain the necessary references.

# Example Message - Copy Case from Server Park to BDB File

```
{
  "serial_number":"1234"
  ,"source_hostname":"blaise-dev-bsp-tel.uksouth.cloudapp.azure.com"
  ,"source_server_park":"TEL-DEV"
  ,"source_instrument":"OPN1901A"
  ,"dest_filepath":"c:\\#test\\"
  ,"dest_instrument":"OPN1901A"
  ,"action":"copy"
}                     
```

# Example Message - Move Case from Server Park to Server Park

```
{
  "serial_number":"1234"
  ,"source_hostname":"blaise-dev-bsp-tel.uksouth.cloudapp.azure.com"
  ,"source_server_park":"TEL-DEV"
  ,"source_instrument":"OPN1901A"
  ,"dest_hostname":"blaise-dev-bsp-val.uksouth.cloudapp.azure.com"
  ,"dest_server_park":"VAL-DEV"
  ,"dest_instrument":"OPN1901A"
  ,"action":"move"
}                     
```

# Example Message - Delete Case from Server Park

```
{
  "serial_number":"1234"
  ,"source_hostname":"blaise-dev-bsp-tel.uksouth.cloudapp.azure.com"
  ,"source_server_park":"TEL-DEV"
  ,"source_instrument":"OPN1901A"
  ,"action":"delete"
}                     
```

# Installing the Service

  - Build the Solution
    - In Visual Studio select "Release" as the Solution Configuration
    - Select the "Build" menu
    - Select "Build Solution" from the "Build" menu
  - Copy the release files (/bin/release/) to the install location on the server
  - Uninstall any previous installs
    - Stop the service from running
    - Open a command prompt as administrator
    - Navigate to the windows service installer location
      - cd c:\Windows\Microsoft.NET\Framework\v4.0.30319\
    - Run installUtil.exe /U from this location and pass it the location of the service executable
      - InstallUtil.exe /U {install location}\BlaiseCaseHandler.exe
  - Run the installer against the release build
    - Open a command prompt as administrator
    - Navigate to the windows service installer location
      - cd c:\Windows\Microsoft.NET\Framework\v4.0.30319\
    - Run installUtil.exe from this location and pass it the location of the service executable
      - InstallUtil.exe {install location}\BlaiseCaseHandler.exe
    - Set the service to delayed start
    - Start the service
