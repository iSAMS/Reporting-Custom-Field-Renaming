﻿
# Custom Field Renaming

## Download

You can download the latest version of the application by checking the Release Tree.

## Configuration

### How do I configure the application for my setup?

Wherever you decide to unpack the latest download, you will need to create a `config.json` file for the application to work. This file must be in the following format:

    {
      "domain": "https://your.isams.cloud",
      "restApiClientId": "Your-Student-API-Client-ID",
      "restApiClientSecret": "Your-Student-API-Client-Secret",
      "targetDirectory": "C://Path/To/Your/Exported/Reports",
      "customFieldName": "Name of your Custom Field"
    }

### Where can I find the details needed for config.json?

**Domain** - This will be the host URL that is used to access your iSAMS instance. This will typically end with `isams.cloud`.

**RestApiClientId** - Each Client within your iSAMS instance will have a unique ID. For the application to work, the chosen Client ID must have access to the `restapi` scope of the `iSAMS.Portal.Student.Api` Client.

**RestAPIClientSecret** - Each Client has a Secret key used to validate a request.

**TargetDirectory** - This is the absolute path to the folder containing your exported reports. Ensure that the directory is not a `ZIP` file.

**CustomFieldName** - This is the display name of the Custom Field within iSAMS that you wish to use. The value of the Custom Field will become the name of each file within your `TargetDirectory`.

You can find a list of Custom Fields by navigating to:

    iSAMS > Pupil/Student Manager > Management Options > Custom Fields

## How Does It Work?

This application can be used to rename any number of files within a directory to the value of a Custom Field.

For a file to be acknowledged by the application the file name must be in a `[SchoolId].pdf` format. Such as `0123456789.pdf`. Any other files will be ignored.
This is the standard format generated by iSAMS when exporting Pupil/Student reports.

The application will then process each of the valid files in the following way -

-   Extract the `SchoolId` from the file name
-   Pass the SchoolId to an API within your iSAMS instance which returns the Custom Fields for the matching Pupil/Student
-   Extract the value belonging to the specified `CustomFieldName`
-   Copy the current file into a `Success` folder within your `TargetDirectory`
-   Rename the copied file to be `[CustomFieldValue].pdf`

Any files which fail to process will be moved into a `Failed` folder within your `TargetDirectory`.

An `event log` will also be created within your `TargetDirectory`.