# Blaise_Case_Updater

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

# Installing the Service

  - Build the Solution 
    - In Visual Studio select "Release" as the Solution Confiiguration
    - Select "Build" from the toolbar
    - Select "Build Solution" from the "Build" dropdown
  - Copy the release files to the program install location on the server
  - Run the installer against the release build
    - Open command prompt as administrator
    - Navigate to the windows service installer location
      - cd c:\Windows\Microsoft.NET\Framework\v4.0.30319\
    - Run installUtil.exe from this location and pass it the location of the service exe.
      - InstallUtil.exe {install location}\BlaiseCaseHandler.exe
