'''
description: create an app as per instructions

'''

**Desciption**
1. name of application:  Docs Validator
2. it is an .NET minimalapi application.
3. the purpose of the app is to validate a PDF file before is uploaded on server.
    - file extension validation, should be a PDF file
    - digital signature validation, the owner of the digitatl signature has to be the actual user
    - before the file is uploaded, to be send to ClamAV server. 
    - file name is changed with an generated name, to avoid exploits.
    - user validation - user can approve or not the file
4. Proccess of the upload, download, validating and signing of the file is managed by a workflow.
5. database is used to keep trace of the status of workflow, file storage, file attributes.
6. directory and files can be accessed and manipulated based on roles, scopes and permissions

**Authorization**
1. Scopes:
    - CanRead
    - CanWrite
    - CanDelete
    - CanUpdate
2. Permissions:
    - All
    - OnlyHis
    - Asigned
**User Roles**
1. administrator - 
    - All:CanRead
    - All:CanWrite
    - All:CanDelete
    - All:CanUpdate
    - All:CanValidate
2. validator
    - Assigned:CanRead
    - Assigned:CanUpdate
    - Assigned:CanValidate
3. expert
    - OnlyHis:CanRead
    - OnlyHis:CanUpdate
    - OnlyHis:CanValidate
    - OnlyHis:CanWrite
    - OnlyHis:CanDelete


**Workflow of the app**
1. user is authenticating / authorizating to the api
2. user upload a digitally signed pdf. user assign a validator to approve the file.
3. workflow is initiated. status of the file is updated and stored 
4. file is validated. status of file changing. if the file is not validated will be not uploaded. warning notice will be issued.
5. if the file is validated and stored, the assigned validator is noticed to validate and approve the file. status of file changing
6. assigned validator will download the file, and sign the file. will upload the file to the server. file is validated.. status of file changing. if is necessary will assign another validator to approve the file. if not the workflow is finished.
5. the proces continue until file is approved.

