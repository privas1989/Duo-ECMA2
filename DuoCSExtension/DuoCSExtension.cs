/****************************** File Header ******************************\
File Name:    DuoCSExtension.cs
Project:      DuoCSExtension
Author:       Pedro Rivas
Email:        admin@rivas.pw

This project will create a dynamic library extension that will allow MIM
(Microsoft Identity Manager) to connect to a Duo MFA environment.

THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\***************************************************************************/


using System;
using System.IO;
using Microsoft.MetadirectoryServices;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace FimSync_Ezma
{
    public class EzmaExtension :
    IMAExtensible2CallExport,
    IMAExtensible2CallImport,
    IMAExtensible2GetSchema,
    IMAExtensible2GetCapabilities,
    IMAExtensible2GetParameters
    {

        public EzmaExtension()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private List<DuoUser> DuoUsers = new List<DuoUser>();
        private int m_importPageSize = 20;
        private int m_importDefaultPageSize = 50;
        private int m_importMaxPageSize = 1000;
        private int m_exportDefaultPageSize = 50;
        private int m_exportMaxPageSize = 1000;
        private int userCount = 0;
        int? offset = 0;
        FimSync_Ezma.DuoApi client;

        log4net.ILog log;

        OperationType m_importOperation;
        OperationType m_exportOperation;

        #region FIM MA setup
        public MACapabilities Capabilities
        {
            get
            {
                MACapabilities myCapabilities = new MACapabilities();
                myCapabilities.ConcurrentOperation = false;
                myCapabilities.ObjectRename = true;
                myCapabilities.DeleteAddAsReplace = true;
                myCapabilities.DeltaImport = false;
                myCapabilities.DistinguishedNameStyle = MADistinguishedNameStyle.Generic;
                myCapabilities.ExportType = MAExportType.AttributeUpdate;
                myCapabilities.NoReferenceValuesInFirstExport = true;
                myCapabilities.Normalizations = MANormalizations.None;
                return myCapabilities;
            }
        }

        public IList<ConfigParameterDefinition> GetConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            List<ConfigParameterDefinition> configParametersDefinitions = new List<ConfigParameterDefinition>();

            switch (page)
            {
                // Connectivity parameters.
                case ConfigParameterPage.Connectivity:
                    configParametersDefinitions.Add(ConfigParameterDefinition.CreateStringParameter("Integration Key", "", ""));
                    configParametersDefinitions.Add(ConfigParameterDefinition.CreateStringParameter("Secret Key", "", ""));
                    configParametersDefinitions.Add(ConfigParameterDefinition.CreateStringParameter("API Host Name", "", ""));
                    break;
                // Global parameters.
                case ConfigParameterPage.Global:
                    break;
                case ConfigParameterPage.Partition:
                    break;
                case ConfigParameterPage.RunStep:
                    break;
            }
            return configParametersDefinitions;
        }

        public ParameterValidationResult ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            ParameterValidationResult myResults = new ParameterValidationResult();
            return myResults;
        }

        public Schema GetSchema(KeyedCollection<string, ConfigParameter> configParameters)
        {
            // Duo schema
            SchemaType userType = SchemaType.Create("user", false);
            //userType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("employeeID", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("username", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("email", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("firstname", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("lastname", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("realname", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("status", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("user_id", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("alias1", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("alias2", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("alias3", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("alias4", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("created", AttributeType.Integer));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("is_enrolled", AttributeType.Boolean));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("notes", AttributeType.String));

            Schema schema = Schema.Create();
            schema.Types.Add(userType);

            return schema;
        }

        public OpenImportConnectionResults OpenImportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            FileInfo finfo = new FileInfo(Utils.ExtensionsDirectory + "\\log4netDuoCS.config");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(finfo);

            m_importOperation = importRunStep.ImportType;
            m_importPageSize = importRunStep.PageSize;

            log.Info("Import connection - start.");

            try
            {
                log.Debug("Attempting to connect to the Duo API.");
                client = new FimSync_Ezma.DuoApi(configParameters["Integration Key"].Value, configParameters["Secret Key"].Value, configParameters["API Host Name"].Value);
                log.Debug("Successful connection to the Duo API.");
            }
            catch (DuoException e)
            {
                log.Error("Error connecting to the Duo API: Duo API Error: " + e.Message);
            }
            catch (Exception e)
            {
                log.Error("Error connecting to the Duo API: Message: " + e.Message);
            }

            log.Info("Import connection - finish.");

            return new OpenImportConnectionResults();
        }

        public GetImportEntriesResults GetImportEntries(GetImportEntriesRunStep importRunStep)
        {
            log.Info("Import entries results - start.");
            List<CSEntryChange> csentries = new List<CSEntryChange>();
            GetImportEntriesResults importReturnInfo = new GetImportEntriesResults();

            if (OperationType.Full == m_importOperation)
            {
                log.Debug("Doing a full import operation.");

                try
                {
                    var parameters = new Dictionary<string, string>();
                    var jsonResponse = client.JSONPagingApiCall("GET", "/admin/v1/users", parameters, (int)offset, m_importPageSize);
                    var pagedUsers = jsonResponse["response"] as System.Collections.ArrayList;
                    System.Console.WriteLine(String.Format("{0} users at offset {1}", pagedUsers.Count, offset));
                    foreach (Dictionary<string, object> user in pagedUsers)
                    {
                        CSEntryChange csentry = CSEntryChange.Create();
                        csentry.ObjectModificationType = ObjectModificationType.Add;
                        csentry.ObjectType = "user";

                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("username", Convert.ToString(user["username"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("firstname", Convert.ToString(user["firstname"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("lastname", Convert.ToString(user["lastname"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("realname", Convert.ToString(user["realname"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("email", Convert.ToString(user["email"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("status", Convert.ToString(user["status"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("user_id", Convert.ToString(user["user_id"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("alias1", Convert.ToString(user["alias1"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("alias2", Convert.ToString(user["alias2"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("alias3", Convert.ToString(user["alias3"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("alias4", Convert.ToString(user["alias4"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("created", Convert.ToInt32(user["created"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("is_enrolled", Convert.ToBoolean(user["is_enrolled"])));
                        csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("notes", Convert.ToString(user["notes"])));

                        csentry.DN = Convert.ToString(user["username"]);

                        csentries.Add(csentry);
                        userCount++;
                    }

                    log.Debug("Processed " + userCount + " Duo users.");

                    var metadata = jsonResponse["metadata"] as Dictionary<string, object>;
                    if (metadata.ContainsKey("next_offset"))
                    {
                        offset = metadata["next_offset"] as int?;
                        importReturnInfo.MoreToImport = true;
                        log.Debug("JSON response contains an offset. More users to import exist.");
                    }
                    else
                    {
                        offset = null;
                        importReturnInfo.MoreToImport = false;
                        log.Debug("JSON response does not contain an offset. No more users to import.");
                    }
                }
                catch (DuoException e)
                {
                    log.Error("Error retriving Duo users: Duo API Error: " + e.Message);
                }
                catch (Exception e)
                {
                    offset = null;
                    log.Error("Error retriving Duo users: Message: " + e.Message + "Stackstrace: " + e.StackTrace);
                }

                log.Info("Finished import operation. " + userCount + " have been processed.");
            }

            if (OperationType.Delta == m_importOperation)
            {
                //Not implemented.
            }
            
            importReturnInfo.CSEntries = csentries;
            log.Info("Import entries results - finish.");
            return importReturnInfo;
        }

        public CloseImportConnectionResults CloseImportConnection(CloseImportConnectionRunStep importRunStepInfo)
        {
            return new CloseImportConnectionResults();
        }

        public void OpenExportConnection(KeyedCollection<string, ConfigParameter> configParameters, Microsoft.MetadirectoryServices.Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            FileInfo finfo = new FileInfo(Utils.ExtensionsDirectory + "\\log4netGoogleMailCS.config");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(finfo);

            log.Info("Export connection - start.");

            try
            {
                log.Debug("Attempting to connect to the Duo API.");
                client = new FimSync_Ezma.DuoApi(configParameters["Integration Key"].Value, configParameters["Secret Key"].Value, configParameters["API Host Name"].Value);
                log.Debug("Successful connection to the Duo API.");
            }
            catch (DuoException e)
            {
                log.Error("Error connecting to the Duo API: Duo API Error: " + e.Message);
            }
            catch (Exception e)
            {
                log.Error("Error connecting to the Duo API: Message: " + e.Message);
            }

            log.Info("Export connection - finish.");
        }

        public PutExportEntriesResults PutExportEntries(IList<CSEntryChange> csentries)
        {
            //
            // The csentries parameter contains a collection of CSEntryChange
            // objects that need to be exported.  The number of CSEntryChange
            // objects is determined by the bacth size set on the Run Profile Step,
            // which contain be obtained from exportRunStep.BatchSize in OpenExportConnection().
            //

            PutExportEntriesResults exportEntriesResults = new PutExportEntriesResults();

            foreach (CSEntryChange csentryChange in csentries)
            {
                //Default is success.
                MAExportError exportResult = MAExportError.Success;
                List<AttributeChange> attributeChanges = new List<AttributeChange>();
                switch (csentryChange.ObjectModificationType)
                {
                    case ObjectModificationType.Add:
                        #region Create User
                        if (csentryChange.ObjectType == "user")
                        {
                            log.Info("Creating new user " + csentryChange.DN);

                            // Parameters
                            var parameters = new Dictionary<string, string>();

                            foreach (AttributeChange ch in csentryChange.AttributeChanges)
                            {
                                log.Debug("Adding " + ch.Name + " " + ch.ValueChanges.Count + " " + ch.ValueChanges[0].Value.ToString());
                                switch (ch.Name)
                                {
                                    case "username":
                                        parameters.Add("username", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "email":
                                        parameters.Add("email", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "firstname":
                                        parameters.Add("firstname", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "lastname":
                                        parameters.Add("lastname", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "realname":
                                        parameters.Add("realname", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "status":
                                        parameters.Add("status", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "alias1":
                                        parameters.Add("alias1", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "alias2":
                                        parameters.Add("alias2", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "alias3":
                                        parameters.Add("alias3", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "alias4":
                                        parameters.Add("alias4", ch.ValueChanges[0].Value.ToString());
                                        break;
                                    case "notes":
                                        parameters.Add("notes", ch.ValueChanges[0].Value.ToString());
                                        break;
                                }
                            }

                            try
                            {
                                log.Debug("Attempting to create user " + csentryChange.DN + ".");
                                var r = client.JSONApiCall<Dictionary<string, object>>("POST", "/admin/v1/users", parameters);

                                if (!String.IsNullOrEmpty(r["created"].ToString()))
                                {
                                    log.Info("Sucessfully created " + csentryChange.DN + "'s Duo account.");
                                }
                                else
                                {
                                    log.Error("Unable to create user " + csentryChange.DN + ". Unspecified error.");
                                    exportResult = MAExportError.ExportErrorMissingProvisioningAttribute;
                                    exportEntriesResults.CSEntryChangeResults.Add(CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult));
                                }
                            }
                            catch (DuoException e)
                            {
                                log.Error("Unable to create user: " + csentryChange.DN + "Duo API Error: " + e.Message);
                                exportResult = MAExportError.ExportErrorMissingProvisioningAttribute;
                                exportEntriesResults.CSEntryChangeResults.Add(CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult));
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.Error("Unable to create user: " + csentryChange.DN + "Error: " + e.Message);
                                exportResult = MAExportError.ExportErrorMissingProvisioningAttribute;
                                exportEntriesResults.CSEntryChangeResults.Add(CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult));
                                continue;
                            }

                            exportEntriesResults.CSEntryChangeResults.Add(CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult));
                        }
                        #endregion

                        break;
                    case ObjectModificationType.Replace:
                    case ObjectModificationType.Update:
                        #region Update User
                        if (csentryChange.ObjectType == "user")
                        {
                            log.Info("Modifying " + csentryChange.DN + ".");
                            var parameters = new Dictionary<string, string>();

                            foreach (AttributeChange ch in csentryChange.AttributeChanges)
                            {
                                //Loop that iterates the different attributes that are changing
                                foreach (ValueChange vch in ch.ValueChanges)
                                {
                                    //loop that iterates the different changes, this usually is an ADD plus a DELETE for
                                    //updating an attribute.
                                    switch (vch.ModificationType)
                                    {
                                        case ValueModificationType.Add:
                                            log.Debug(String.Format("New value for {0} => {1}", ch.Name, vch.Value.ToString()));
                                            switch (ch.Name)
                                            {
                                                case "username":
                                                    parameters.Add("username", vch.Value.ToString());
                                                    break;
                                                case "email":
                                                    parameters.Add("email", vch.Value.ToString());
                                                    break;
                                                case "firstname":
                                                    parameters.Add("firstname", vch.Value.ToString());
                                                    break;
                                                case "lastname":
                                                    parameters.Add("lastname", vch.Value.ToString());
                                                    break;
                                                case "realname":
                                                    parameters.Add("realname", vch.Value.ToString());
                                                    break;
                                                case "status":
                                                    parameters.Add("status", vch.Value.ToString());
                                                    break;
                                                case "alias1":
                                                    parameters.Add("alias1", vch.Value.ToString());
                                                    break;
                                                case "alias2":
                                                    parameters.Add("alias2", vch.Value.ToString());
                                                    break;
                                                case "alias3":
                                                    parameters.Add("alias3", vch.Value.ToString());
                                                    break;
                                                case "alias4":
                                                    parameters.Add("alias4", vch.Value.ToString());
                                                    break;
                                                case "notes":
                                                    parameters.Add("notes", vch.Value.ToString());
                                                    break;
                                            }
                                            break;
                                        case ValueModificationType.Delete:
                                            log.Debug(String.Format("Old value for {0} => {1}", ch.Name, vch.Value.ToString()));
                                            break;
                                    }
                                }
                            }

                            try
                            {
                                log.Debug("Attempting to modify user " + csentryChange.DN + ".");
                                var r = client.JSONApiCall<Dictionary<string, object>>("POST", "/admin/v1/users/" + getDuoID(csentryChange.DN), parameters);

                                if (!String.IsNullOrEmpty(r["created"].ToString()))
                                {
                                    log.Info("Sucessfully modified " + csentryChange.DN + "'s Duo account.");
                                }
                                else
                                {
                                    log.Error("Unable to modify user " + csentryChange.DN + ". Unspecified error.");
                                    exportResult = MAExportError.ExportErrorMissingProvisioningAttribute;
                                    exportEntriesResults.CSEntryChangeResults.Add(CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult));
                                }
                            }
                            catch (DuoException e)
                            {
                                log.Error("Unable to modify user: " + csentryChange.DN + "Duo API Error: " + e.Message);
                                exportResult = MAExportError.ExportErrorConnectedDirectoryError;
                                exportEntriesResults.CSEntryChangeResults.Add(
                                    CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult, "Duo API Error", e.Message + "||" + e.StackTrace));
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.Error("Unable to modify user: " + csentryChange.DN + "Error: " + e.Message);
                                exportResult = MAExportError.ExportErrorCustomContinueRun;
                                exportEntriesResults.CSEntryChangeResults.Add(
                                    CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult, "Unexpected error", "IDFK"));
                                continue;
                            }
                        }
                        #endregion
                        break;
                    case ObjectModificationType.Delete:
                        #region Delete User
                        if (csentryChange.ObjectType == "user")
                        {
                            log.Info("Deleting " + csentryChange.DN + ".");
                            var parameters = new Dictionary<string, string>();
                            try
                            {
                                log.Debug("Attempting to delete user " + csentryChange.DN + ".");
                                var r = client.JSONApiCall<Dictionary<string, object>>("DELETE", "/admin/v1/users/" + getDuoID(csentryChange.DN), parameters);
                                log.Info("Successfully deleted user " + csentryChange.DN + ".");
                            }
                            catch (DuoException e)
                            {
                                log.Error("Unable to delete user: " + csentryChange.DN + "Duo API Error: " + e.Message);
                                exportResult = MAExportError.ExportErrorConnectedDirectoryError;
                                exportEntriesResults.CSEntryChangeResults.Add(
                                    CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult, "Duo API Error", e.Message + "||" + e.StackTrace));
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.Error("Unable to delete user: " + csentryChange.DN + "Error: " + e.Message);
                                exportResult = MAExportError.ExportErrorCustomContinueRun;
                                exportEntriesResults.CSEntryChangeResults.Add(
                                    CSEntryChangeResult.Create(csentryChange.Identifier, attributeChanges, exportResult, "Unexpected error", "IDFK"));
                                continue;
                            }
                        }
                        #endregion
                        break;
                    default:
                        break;
                }
            }

            return exportEntriesResults;
        }

        public void CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
        }

        public int ExportDefaultPageSize
        {
            get
            {
                return m_exportDefaultPageSize;
            }
            set
            {
                m_exportDefaultPageSize = value;
            }
        }

        public int ExportMaxPageSize
        {
            get
            {
                return m_exportMaxPageSize;
            }
            set
            {
                m_exportMaxPageSize = value;
            }
        }

        public int ImportMaxPageSize
        {
            get
            {
                return m_importMaxPageSize;
            }
        }

        public int ImportDefaultPageSize
        {
            get
            {
                return m_importDefaultPageSize;
            }
        }

        #endregion

        #region Helper functions
        private string getDuoID(string userID)
        {
            string DuoID = "";

            log.Debug("Getting a Duo ID for " + userID + ".");
            var parameters = new Dictionary<string, string>();
            parameters.Add("username", userID);

            try
            {
                log.Debug("Attempting to retrieve the user ID for " + userID + ".");
                var r = client.JSONApiCall<System.Collections.ArrayList>("GET", "/admin/v1/users", parameters);

                if (r.Count == 1)
                {
                    Dictionary<string, object> user = (Dictionary<string, object>)r[0];
                    DuoID = user["user_id"].ToString();
                    log.Debug("Successfully retrieved the Duo ID for " + userID + ".");
                }
                else
                {
                    log.Debug("Retrieved an empty Duo ID for " + userID + ".");
                }
            }
            catch (DuoException e)
            {
                log.Error("Unable to retrieve the user ID for : " + userID + ". Duo API Error: " + e.Message);      
            }
            catch (Exception e)
            {
                log.Error("Unable to retrieve the user ID for : " + userID + ". Error: " + e.Message);
            }

            return DuoID;
        }

        #endregion
    };
}
