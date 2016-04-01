/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Management.Automation;
using System.Globalization;
using System.Management.Automation.Internal;
using System.Management.Automation.Help;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// This class implements get-help command
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Help", DefaultParameterSetName = "AllUsersView", HelpUri = "http://go.microsoft.com/fwlink/?LinkID=113316")]
    public sealed class GetHelpCommand : PSCmdlet
    {
        /// <summary>
        /// Help Views
        /// </summary>
        internal enum HelpView
        {
            Default      = 0x00, // Default View
            DetailedView = 0x01,
            FullView     = 0x02,
            ExamplesView = 0x03 
        }

        /// <summary>
        /// Default constructor for the GetHelpCommand class
        /// </summary>
        public GetHelpCommand()
        {
        }

        #region Cmdlet Parameters

        private string _name = "";
        /// <summary>
        /// Target to search for help
        /// </summary>
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        private string _path;
        /// <summary>
        /// Path to provider location that user is curious about.
        /// </summary>
        [Parameter]
        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
            }
        }

        private string[] _category;
        /// <summary>
        /// List of help categories to search for help
        /// </summary>
        [Parameter]
        [ValidateSet(
            "Alias", "Cmdlet", "Provider", "General", "FAQ", "Glossary", "HelpFile", "ScriptCommand", "Function", "Filter", "ExternalScript", "All", "DefaultHelp", "Workflow", "DscResource", "Class", "Configuration",
             IgnoreCase = true)]
        public string[] Category
        {
            get
            {
                return _category;
            }
            set
            {
                _category = value;
            }
        }

        private string[] _component = null;

        /// <summary>
        /// List of Component's to search on.
        /// </summary>
        /// <value></value>
        [Parameter]
        public string[] Component
        {
            get
            {
                return _component;
            }
            set
            {
                _component = value;
            }
        }

        private string[] _functionality = null;

        /// <summary>
        /// List of Functionality's to search on.
        /// </summary>
        /// <value></value>
        [Parameter]
        public string[] Functionality
        {
            get
            {
                return _functionality;
            }
            set
            {
                _functionality = value;
            }
        }

        private string[] _role = null;

        /// <summary>
        /// List of Role's to search on.
        /// </summary>
        /// <value></value>
        [Parameter]
        public string[] Role
        {
            get
            {
                return _role;
            }
            set
            {
                _role = value;
            }
        }

        private string _provider = "";

        /// <summary>
        /// Changes the view of HelpObject returned
        /// </summary>
        /// <remarks>
        /// Currently we support following views:
        /// 
        /// 1. Reminder (Default - Experienced User)
        /// 2. Detailed (Beginner - Beginning User)
        /// 3. Full     (All Users)
        /// 4. Examples 
        /// 5. Parameters 
        /// 
        /// Currently we support these views only for Cmdlets.
        /// A SnapIn developer can however change these views.
        /// </remarks>
        [Parameter(ParameterSetName = "DetailedView", Mandatory = true)]
        public SwitchParameter Detailed
        {
            set
            {
                if (value.ToBool())
                {
                    _viewTokenToAdd = HelpView.DetailedView;
                }
            }
        }

        /// <summary>
        /// Changes the view of HelpObject returned
        /// </summary>
        /// <remarks>
        /// Currently we support following views:
        /// 
        /// 1. Reminder (Default - Experienced User)
        /// 2. Detailed (Beginner - Beginning User)
        /// 3. Full     (All Users)
        /// 4. Examples 
        /// 5. Parameters 
        /// 
        /// Currently we support these views only for Cmdlets.
        /// A SnapIn developer can however change these views.
        /// </remarks>
        [Parameter(ParameterSetName = "AllUsersView")]
        public SwitchParameter Full
        {
            set
            {
                if (value.ToBool())
                {
                    _viewTokenToAdd = HelpView.FullView;
                }
            }
        }

        /// <summary>
        /// Changes the view of HelpObject returned
        /// </summary>
        /// <remarks>
        /// Currently we support following views:
        /// 
        /// 1. Reminder (Default - Experienced User)
        /// 2. Detailed (Beginner - Beginning User)
        /// 3. Full     (All Users)
        /// 4. Examples 
        /// 
        /// Currently we support these views only for Cmdlets.
        /// A SnapIn developer can however change these views.
        /// </remarks>
        [Parameter(ParameterSetName = "Examples", Mandatory = true)]
        public SwitchParameter Examples
        {
            set
            {
                if (value.ToBool())
                {
                    _viewTokenToAdd = HelpView.ExamplesView;
                }
            }
        }

        /// <summary>
        /// Parameter name.
        /// </summary>
        /// <remarks>
        /// Support WildCard strings as supported by WildcardPattern class.
        /// </remarks>
        [Parameter(ParameterSetName = "Parameters", Mandatory = true)]
        public string Parameter
        {
            set
            {
                _parameter = value;
            }
            get
            {
                return _parameter;
            }
        }
        private string _parameter;

        /// <summary>
        /// This parameter,if true, will direct get-help cmdlet to
        /// navigate to a URL (stored in the command MAML file under
        /// the uri node).
        /// </summary>
        [Parameter(ParameterSetName = "Online", Mandatory = true)]
        public SwitchParameter Online
        {
            set
            {
                showOnlineHelp = value;
                if (showOnlineHelp)
                {
                    VerifyParameterForbiddenInRemoteRunspace(this, "Online");
                }
            }
            get
            {
                return showOnlineHelp;
            }
        }
        private bool showOnlineHelp;

        private bool showWindow;
        /// <summary>
        /// Gets and sets a value indicatuing whether the help should be displayed in a separate window
        /// </summary>
        [Parameter(ParameterSetName = "ShowWindow", Mandatory = true)]
        public SwitchParameter ShowWindow
        {
            get
            {
                return showWindow;
            }

            set
            {
                showWindow = value;
                if (showWindow)
                {
                    VerifyParameterForbiddenInRemoteRunspace(this, "ShowWindow");
                }
            }
        }

        // The following variable controls the view.
        private HelpView _viewTokenToAdd = HelpView.Default;

#if !CORECLR
        private GraphicalHostReflectionWrapper graphicalHostReflectionWrapper;
#endif

        #endregion

        #region Cmdlet API implementation

        /// <summary>
        /// Implements the BeginProcessing() method for get-help command
        /// </summary>
        protected override void BeginProcessing()
        {
            if (!Online.IsPresent && UpdatableHelpSystem.ShouldPromptToUpdateHelp() && HostUtilities.IsProcessInteractive(MyInvocation) && HasInternetConnection())
            {
                if(ShouldContinue(HelpDisplayStrings.UpdateHelpPromptBody, HelpDisplayStrings.UpdateHelpPromptTitle))
                {
                    System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace).AddCommand("Update-Help").Invoke();
                }
                    
                UpdatableHelpSystem.SetDisablePromptToUpdateHelp();
            }
        }

        /// <summary>
        /// Implements the ProcessRecord() method for get-help command
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
#if !CORECLR
                if(this.ShowWindow)
                {
                    this.graphicalHostReflectionWrapper = GraphicalHostReflectionWrapper.GetGraphicalHostReflectionWrapper(this, "Microsoft.PowerShell.Commands.Internal.HelpWindowHelper");
                }
#endif
                this.Context.HelpSystem.OnProgress += new HelpSystem.HelpProgressHandler(HelpSystem_OnProgress);

                bool failed = false;
                HelpCategory helpCategory = ToHelpCategory(this._category, ref failed);

                if (failed)
                    return;

                // Validate input parameters
                ValidateAndThrowIfError(helpCategory);

                HelpRequest helpRequest = new HelpRequest(this.Name, helpCategory);

                helpRequest.Provider = this._provider;
                helpRequest.Component = this._component;
                helpRequest.Role = this._role;
                helpRequest.Functionality = this._functionality;
                helpRequest.ProviderContext = new ProviderContext(
                    this.Path,
                    this.Context.Engine.Context,
                    this.SessionState.Path);
                helpRequest.CommandOrigin = this.MyInvocation.CommandOrigin;

                // the idea is to use yield statement in the help lookup to speed up
                // perceived user experience....So HelpSystem.GetHelp returns an
                // IEnumerable..
                IEnumerable<HelpInfo> helpInfos = this.Context.HelpSystem.GetHelp(helpRequest);
                // HelpCommand acts differently when there is just one help object and when
                // there are more than one object...so handling this behavior through
                // some variables.
                HelpInfo firstHelpInfoObject = null;
                int countOfHelpInfos = 0;
                foreach (HelpInfo helpInfo in helpInfos)
                {
                    // honor Ctrl-C from user.
                    if (IsStopping)
                    {
                        return;
                    }

                    if (0 == countOfHelpInfos)
                    {
                        firstHelpInfoObject = helpInfo;
                    }
                    else
                    {
                        // write first help object only once.
                        if (null != firstHelpInfoObject)
                        {
                            WriteObjectsOrShowOnlineHelp(firstHelpInfoObject, false);
                            firstHelpInfoObject = null;
                        }

                        WriteObjectsOrShowOnlineHelp(helpInfo, false);
                    }
                    countOfHelpInfos++;
                }

                // Write full help as there is only one help info object
                if (1 == countOfHelpInfos)
                {
                    WriteObjectsOrShowOnlineHelp(firstHelpInfoObject, true);
                }
                else if (showOnlineHelp && (countOfHelpInfos > 1))
                {
                    throw PSTraceSource.NewInvalidOperationException(HelpErrors.MultipleOnlineTopicsNotSupported, "Online");
                }

                // show errors only if there is no wildcard search or VerboseHelpErrors is true.
                if (((countOfHelpInfos == 0) && (!WildcardPattern.ContainsWildcardCharacters(helpRequest.Target)))
                    || this.Context.HelpSystem.VerboseHelpErrors)
                {
                    // Check if there is any error happened. If yes, 
                    // pipe out errors. 
                    if (this.Context.HelpSystem.LastErrors.Count > 0)
                    {
                        foreach (ErrorRecord errorRecord in this.Context.HelpSystem.LastErrors)
                        {
                            WriteError(errorRecord);
                        }
                    }
                }
            }
            finally
            {
                this.Context.HelpSystem.OnProgress -= new HelpSystem.HelpProgressHandler(HelpSystem_OnProgress);
                // finally clear the ScriptBlockAst -> Token[] cache
                this.Context.HelpSystem.ClearScriptBlockTokenCache();
            }
        }

        private HelpCategory ToHelpCategory(string[] category, ref bool failed)
        {
            if (category == null || category.Length == 0)
                return HelpCategory.None;

            HelpCategory helpCategory = HelpCategory.None;

            failed = false;

            for (int i = 0; i < category.Length; i++)
            {
                try
                {
                    HelpCategory temp = (HelpCategory)Enum.Parse(typeof(HelpCategory), category[i], true);

                    helpCategory |= temp;
                }
                catch (ArgumentException argumentException)
                {
                    Exception e = new HelpCategoryInvalidException(category[i], argumentException);
                    ErrorRecord errorRecord = new ErrorRecord(e, "InvalidHelpCategory", ErrorCategory.InvalidArgument, null);
                    this.WriteError(errorRecord);

                    failed = true;
                }
            }

            return helpCategory;
        }

        /// <summary>
        /// Change <paramref name="originalHelpObject"/> as per user request.
        /// 
        /// This method creates a new type to the existing typenames 
        /// depending on Detailed,Full,Example parameteres and adds this 
        /// new type(s) to the top of the list.
        /// </summary>
        /// <param name="originalHelpObject">Full help object to transform.</param>
        /// <returns>Transformed help object with new TypeNames.</returns>
        /// <remarks>If Detailed and Full are not specified, nothing is changed.</remarks>
        private PSObject TransformView(PSObject originalHelpObject)
        {
            Diagnostics.Assert(originalHelpObject != null,
                "HelpObject should not be null");

            if (_viewTokenToAdd == HelpView.Default)
            {
                tracer.WriteLine("Detailed, Full, Examples are not selected. Constructing default view.");
                return originalHelpObject;
            }

            string tokenToAdd = _viewTokenToAdd.ToString();
            // We are changing the types without modifying the original object.
            // The contract between help command and helpsystem does not 
            // allow us to modify returned help objects.
            PSObject objectToReturn = originalHelpObject.Copy();
            objectToReturn.TypeNames.Clear();

            if (originalHelpObject.TypeNames.Count == 0)
            {
                string typeToAdd = string.Format(CultureInfo.InvariantCulture, "HelpInfo#{0}", tokenToAdd);
                objectToReturn.TypeNames.Add(typeToAdd);
            }
            else
            {
                // User request at the top..
                foreach (string typeName in originalHelpObject.TypeNames)
                {
                    // dont add new types for System.String and System.Object..
                    // as they are handled differently for F&0..(bug935095)
                    if (typeName.ToLowerInvariant().Equals("system.string") ||
                        typeName.ToLowerInvariant().Equals("system.object"))
                    {
                        continue;
                    }

                    string typeToAdd = string.Format(CultureInfo.InvariantCulture, "{0}#{1}", typeName, tokenToAdd);
                    tracer.WriteLine("Adding type {0}", typeToAdd);
                    objectToReturn.TypeNames.Add(typeToAdd);
                }

                // Existing typenames at the bottom..
                foreach (string typeName in originalHelpObject.TypeNames)
                {
                    tracer.WriteLine("Adding type {0}", typeName);
                    objectToReturn.TypeNames.Add(typeName);
                }
            }

            return objectToReturn;
        }

        /// <summary>
        /// Gets the paramater info for patterns identified Parameter property.
        /// Writes the parameter info(s) to the output stream. An error is thrown
        /// if a parameter with a given pattern is not found.
        /// </summary>
        /// <param name="helpInfo">HelpInfo Object to look for the parameter.</param>
        private void GetAndWriteParameterInfo(HelpInfo helpInfo)
        {
            tracer.WriteLine("Searching parameters for {0}", helpInfo.Name);
            PSObject[] pInfos = helpInfo.GetParameter(this._parameter);

            if ((pInfos == null) || (pInfos.Length == 0))
            {
                Exception innerException = PSTraceSource.NewArgumentException("Parameter",
                    HelpErrors.NoParmsFound, this._parameter);
                WriteError(new ErrorRecord(innerException,"NoParmsFound", ErrorCategory.InvalidArgument, helpInfo));
            }
            else
            {
                foreach (PSObject pInfo in pInfos)
                {
                    WriteObject(pInfo);
                }
            }
        }

        /// <summary>
        /// Validates input parameters
        /// </summary>
        /// <param name="cat">Category specified by the user</param>
        /// <exception cref="ArgumentException">
        /// If the request cant be serviced.
        /// </exception>
        private void ValidateAndThrowIfError(HelpCategory cat)
        {
            if (cat == HelpCategory.None)
            {
                return;
            }

            // categories that support -Parameter, -Role, -Functionality, -Component parameters
            HelpCategory supportedCategories = 
                HelpCategory.Alias | HelpCategory.Cmdlet | HelpCategory.ExternalScript | 
                HelpCategory.Filter | HelpCategory.Function | HelpCategory.ScriptCommand | HelpCategory.Workflow;

            if ((cat & supportedCategories) == 0)
            {
                if (!string.IsNullOrEmpty(this._parameter))
                {
                    throw PSTraceSource.NewArgumentException("Parameter",
                        HelpErrors.ParamNotSupported, "-Parameter");
                }

                if (this._component != null)
                {
                    throw PSTraceSource.NewArgumentException("Component",
                        HelpErrors.ParamNotSupported, "-Component");
                }

                if (this._role != null)
                {
                    throw PSTraceSource.NewArgumentException("Role",
                        HelpErrors.ParamNotSupported, "-Role");
                }

                if (this._functionality != null)
                {
                    throw PSTraceSource.NewArgumentException("Functionality",
                        HelpErrors.ParamNotSupported, "-Functionality");
                }
            }
        }

        /// <summary>
        /// Helper method used to Write the help object onto the output
        /// stream or show online help (URI extracted from the HelpInfo)
        /// object.
        /// </summary>
        private void WriteObjectsOrShowOnlineHelp(HelpInfo helpInfo, bool showFullHelp)
        {
            if (helpInfo != null)
            {
                // online help can be showed only if showFullHelp is true..
                // showFullHelp will be false when the help tries to display multiple help topics..
                // -Online should not work when multiple help topics are displayed.
                if (showFullHelp && showOnlineHelp)
                {
                    bool onlineUriFound = false;
                    // show online help
                    tracer.WriteLine("Preparing to show help online.");
                    Uri onlineUri = helpInfo.GetUriForOnlineHelp();
                    if (null != onlineUri)
                    {
                        onlineUriFound = true;
                        LaunchOnlineHelp(onlineUri);
                        return;
                    }

                    if (!onlineUriFound)
                    {
                        throw PSTraceSource.NewInvalidOperationException(HelpErrors.NoURIFound); 
                    }
                }
                else if (showFullHelp && ShowWindow)
                {
#if !CORECLR
                    graphicalHostReflectionWrapper.CallStaticMethod("ShowHelpWindow", helpInfo.FullHelp, this);
#endif
                }
                else
                {
                    // show inline help
                    if (showFullHelp)
                    {
                        if (!string.IsNullOrEmpty(_parameter))
                        {
                            GetAndWriteParameterInfo(helpInfo);
                        }
                        else
                        {
                            PSObject objectToReturn = TransformView(helpInfo.FullHelp);
                            objectToReturn.IsHelpObject = true;
                            WriteObject(objectToReturn);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(_parameter))
                        {
                            PSObject[] pInfos = helpInfo.GetParameter(_parameter);
                            if ((pInfos == null) || (pInfos.Length == 0))
                            {
                                return;
                            }
                        }

                        WriteObject(helpInfo.ShortHelp);
                    }
                }
            }
        }

        /// <summary>
        /// Opens the Uri. System's default application will be used
        /// to show the uri.
        /// </summary>
        /// <param name="uriToLaunch"></param>
        private void LaunchOnlineHelp(Uri uriToLaunch)
        {
            Diagnostics.Assert(null != uriToLaunch, "uriToLaunch should not be null");

            if (!uriToLaunch.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !uriToLaunch.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                throw PSTraceSource.NewInvalidOperationException(HelpErrors.ProtocolNotSupported,
                    uriToLaunch.ToString(),
                    "http",
                    "https");
            }

            Exception exception = null;
            try
            {
                this.WriteVerbose(string.Format(CultureInfo.InvariantCulture, HelpDisplayStrings.OnlineHelpUri, uriToLaunch.OriginalString));
                System.Diagnostics.Process browserProcess = new System.Diagnostics.Process();
                browserProcess.StartInfo.UseShellExecute = true;
                browserProcess.StartInfo.FileName = uriToLaunch.OriginalString;
                browserProcess.Start();
            }
            catch (InvalidOperationException ioe)
            {
                exception = ioe;
            }
            catch (System.ComponentModel.Win32Exception we)
            {
                exception = we;
            }

            if (null != exception)
            {
                throw PSTraceSource.NewInvalidOperationException(exception, HelpErrors.CannotLaunchURI, uriToLaunch.OriginalString);
            }
        }

        #endregion

        void HelpSystem_OnProgress(object sender, HelpProgressInfo arg)
        {
            ProgressRecord record = new ProgressRecord(0, this.CommandInfo.Name, arg.Activity);

            record.PercentComplete = arg.PercentComplete;

            WriteProgress(record);
        }

#if !CORECLR
        [DllImport("wininet.dll")]
        static extern bool InternetGetConnectedState(out int desc, int reserved) ;
#endif
        /// <summary>
        /// Checks if we can connect to the internet
        /// </summary>
        /// <returns></returns>
        private bool HasInternetConnection()
        {
#if CORECLR
            return true; // TODO:CORECLR wininet.dll is not present on NanoServer
#else
            int unused;
            return InternetGetConnectedState(out unused, 0 ); 
#endif
        }

        #region Helper methods for verification of parameters against NoLanguage mode

        internal static void VerifyParameterForbiddenInRemoteRunspace(Cmdlet cmdlet, string parameterName)
        {
            if (NativeCommandProcessor.IsServerSide)
            {
                string message = StringUtil.Format(CommandBaseStrings.ParameterNotValidInRemoteRunspace,
                    cmdlet.MyInvocation.InvocationName,
                    parameterName);
                Exception e = new InvalidOperationException(message);
                ErrorRecord errorRecord = new ErrorRecord(e, "ParameterNotValidInRemoteRunspace", ErrorCategory.InvalidArgument, null);
                cmdlet.ThrowTerminatingError(errorRecord);
            }
        }

        #endregion

        #region trace

        [TraceSourceAttribute("GetHelpCommand ", "GetHelpCommand ")]
        static private PSTraceSource tracer = PSTraceSource.GetTracer("GetHelpCommand ", "GetHelpCommand ");

        #endregion
    }

    /// <summary>
    /// Helper methods used as powershell extension from a types file.
    /// </summary>
    public static class GetHelpCodeMethods
    {
        /// <summary>
        /// Verifies if the InitialSessionState of the current process
        /// </summary>
        /// <returns></returns>
        private static bool DoesCurrentRunspaceIncludeCoreHelpCmdlet()
        {

            InitialSessionState iss =
                System.Management.Automation.Runspaces.Runspace.DefaultRunspace.InitialSessionState;
            if (iss != null)
            {
                IEnumerable<SessionStateCommandEntry> publicGetHelpEntries = iss
                    .Commands["Get-Help"]
                    .Where(entry => entry.Visibility == SessionStateEntryVisibility.Public);
                if (publicGetHelpEntries.Count() != 1)
                {
                    return false;
                }

                foreach (SessionStateCommandEntry getHelpEntry in publicGetHelpEntries)
                {
                    SessionStateCmdletEntry getHelpCmdlet = getHelpEntry as SessionStateCmdletEntry;
                    if ((null != getHelpCmdlet) && (getHelpCmdlet.ImplementingType.Equals(typeof (GetHelpCommand))))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieves the HelpUri given a CommandInfo instance.
        /// </summary>
        /// <param name="commandInfoPSObject">
        /// CommandInfo instance wrapped as PSObject
        /// </param>
        /// <returns>
        /// null if <paramref name="commandInfoPSObject"/> is not a CommandInfo type.
        /// null if HelpUri could not be retrieved either from CommandMetadata or
        /// help content.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1055:UriReturnValuesShouldNotBeStrings")]
        public static string GetHelpUri(PSObject commandInfoPSObject)
        {
            if (null == commandInfoPSObject)
            {
                return string.Empty;
            }
            CommandInfo cmdInfo = PSObject.Base(commandInfoPSObject) as CommandInfo;
            // GetHelpUri helper method is expected to be used only by System.Management.Automation.CommandInfo
            // objects from types.ps1xml
            if ((null == cmdInfo) || (string.IsNullOrEmpty(cmdInfo.Name)))
            {
                return string.Empty;
            }

            // The type checking is needed to avoid a try..catch exception block as
            // the CommandInfo.CommandMetadata throws an InvalidOperationException
            // instead of returning null.
            if ((cmdInfo is CmdletInfo) || (cmdInfo is FunctionInfo) || 
                (cmdInfo is ExternalScriptInfo) || (cmdInfo is ScriptInfo))
            {
                if (!string.IsNullOrEmpty(cmdInfo.CommandMetadata.HelpUri))
                {
                    return cmdInfo.CommandMetadata.HelpUri;
                }
            }

            AliasInfo aliasInfo = cmdInfo as AliasInfo;
            if ((null != aliasInfo) && 
                (null != aliasInfo.ExternalCommandMetadata) &&
                (!string.IsNullOrEmpty(aliasInfo.ExternalCommandMetadata.HelpUri)))
            {
                return aliasInfo.ExternalCommandMetadata.HelpUri;
            }

            // if everything else fails..depend on Get-Help infrastructure to get us the Uri.
            string cmdName = cmdInfo.Name;
            if (!string.IsNullOrEmpty(cmdInfo.ModuleName))
            {
                cmdName = string.Format(CultureInfo.InvariantCulture,
                                        "{0}\\{1}", cmdInfo.ModuleName, cmdInfo.Name);
            }

            if (DoesCurrentRunspaceIncludeCoreHelpCmdlet())
            {
                // Win8: 651300 if core get-help is present in the runspace (and it is the only get-help command), use
                // help system directly and avoid perf penalty.
                var currentContext = System.Management.Automation.Runspaces.LocalPipeline.GetExecutionContextFromTLS();
                if ((null != currentContext) && (null != currentContext.HelpSystem))
                {
                    HelpRequest helpRequest = new HelpRequest(cmdName, cmdInfo.HelpCategory);
                    helpRequest.ProviderContext = new ProviderContext(
                        string.Empty,
                        currentContext,
                        currentContext.SessionState.Path);
                    helpRequest.CommandOrigin = CommandOrigin.Runspace;
                    foreach (
                        Uri result in
                            currentContext.HelpSystem.ExactMatchHelp(helpRequest).Select(
                                helpInfo => helpInfo.GetUriForOnlineHelp()).Where(result => null != result))
                    {
                        return result.OriginalString;
                    }
                }
            }
            else
            {
                // win8: 546025. Using Get-Help as command, instead of calling HelpSystem.ExactMatchHelp
                // for the following reasons:
                // 1. Exchange creates proxies for Get-Command and Get-Help in their scenario
                // 2. This method is primarily used to get uri faster while serializing the CommandInfo objects (from Get-Command)
                // 3. Exchange uses Get-Help proxy to not call Get-Help cmdlet at-all while serializing CommandInfo objects
                // 4. Using HelpSystem directly will not allow Get-Help proxy to do its job.
                System.Management.Automation.PowerShell getHelpPS = System.Management.Automation.PowerShell.Create(
                    RunspaceMode.CurrentRunspace).AddCommand("get-help").
                    AddParameter("Name", cmdName).AddParameter("Category",
                                                                cmdInfo.HelpCategory.ToString());
                try
                {
                    Collection<PSObject> helpInfos = getHelpPS.Invoke();

                    if (null != helpInfos)
                    {
                        for (int index = 0; index < helpInfos.Count; index++)
                        {
                            HelpInfo helpInfo;
                            if (LanguagePrimitives.TryConvertTo<HelpInfo>(helpInfos[index], out helpInfo))
                            {
                                Uri result = helpInfo.GetUriForOnlineHelp();
                                if (null != result)
                                {
                                    return result.OriginalString;
                                }
                            }
                            else
                            {
                                Uri result = BaseCommandHelpInfo.GetUriFromCommandPSObject(helpInfos[index]);
                                return (result != null) ? result.OriginalString : string.Empty;
                            }
                        }
                    }
                }
                finally
                {
                    getHelpPS.Dispose();
                }
            }

            return string.Empty;
        }
    }
}
