# Blaise_Case_Handler

Blaise Case Handler is a Windows service intended to run on Windows Server hosting a Blaise 5 server install. The service connects and listens for messages held on a RabbitMQ queue. The messages should contain details of cases (records) to be copied or moved between Blaise databases, see examples below. The service will then use the Blaise API to copy or move these cases between Blaise databases. The service has been updated to support deletion of cases from Blaise databases.

# Setup Development Environment

Clone the git respository to your IDE of choice. Visual Studio 2019 is recomended.

Rename the App.config file to App.local.config and populate the key values. **Never use App.config for development.**

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
  - Copy the release files (/bin/release/) to the program install location on the server
  - Run the installer against the release build
    - Open command prompt as administrator
    - Navigate to the windows service installer location
      - cd c:\Windows\Microsoft.NET\Framework\v4.0.30319\
    - Run installUtil.exe from this location and pass it the location of the service executable.
      - InstallUtil.exe {install location}\BlaiseCaseHandler.exe
