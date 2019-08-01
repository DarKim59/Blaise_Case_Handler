# Blaise_Case_Handler

Blaise Case Handler is a Windows service intended to run on Windows Server. It connects and listens for messages held on a RabbitMQ queue. The messages should contain details of cases (records) to be copied or moved between Blaise databases, see examples below. The service will then use the Blaise API to copy or move these cases between Blaise databases.

# Setup Development Environment

Clone the git respository to your IDE of choice. Visual Studio is recomended.

Populate the key values in the App.config file.

Install the RabbitMQ Client and Log4Net packages via NuGet Package Manager. Console commands:

  ```
  Install-Package RabbitMQ.Client
  Install-Package log4net
  ```

Ensure you have the latest version of Blaise 5 installed from the Statistics Netherlands FTP.

To add the Blaise APIs:
  - Right Click the "References" object under the project in the Visual Studio Solution Explorer
  - Select "Add Reference"
  - Use the "Browse" tab and navigate to "C:\Program Files (x86)\StatNeth\Blaise5\Bin"
  - In this folder all the required APIs for Blaise interaction are available

# Example Message - Server Park to BDB File

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

# Example Message - Server Park to Server Park

```
{
  "serial_number":"1234"
  ,"source_hostname":"blaise-dev-bsp-tel.uksouth.cloudapp.azure.com"
  ,"source_server_park":"TEL-DEV"
  ,"source_instrument":"OPN1901A"
  ,"dest_hostname":"blaise-dev-bsp-val.uksouth.cloudapp.azure.com"
  ,"dest_server_park":"VAL-DEV"
  ,"dest_instrument":"OPN1901A"
  ,"action":"copy"
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
