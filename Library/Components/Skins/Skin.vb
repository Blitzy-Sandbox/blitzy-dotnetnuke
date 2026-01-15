'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'
Imports System.Collections.Specialized
Imports System.IO
Imports System.Web.UI

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Actions
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Entities.Modules.Communications
Imports DotNetNuke.Security.Permissions

'Legacy Support
Namespace DotNetNuke

    <Obsolete("This class is obsolete.  Please use DotNetNuke.UI.Skins.Skin.")> _
    Public Class Skin
        Inherits DotNetNuke.UI.Skins.Skin
    End Class

End Namespace

Namespace DotNetNuke.UI.Skins

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Namespace: DotNetNuke.UI.Skins
    ''' Class	 : Skin
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' Skin is the base for the Skins 
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/04/2005	Documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class Skin

        Inherits Framework.UserControlBase

#Region "Private Members"

        Private _actionEventListeners As ArrayList
        Private objCommunicator As New ModuleCommunicate
        Private m_blnHasModuleEditPermissions As Boolean = False

        'Localized Strings
        Private PANE_LOAD_ERROR As String = Localization.GetString("PaneNotFound.Error")
        Private CONTRACTEXPIRED_ERROR As String = Localization.GetString("ContractExpired.Error")
        Private TABACCESS_ERROR As String = Localization.GetString("TabAccess.Error")
        Private MODULEACCESS_ERROR As String = Localization.GetString("ModuleAccess.Error")
        Private CRITICAL_ERROR As String = Localization.GetString("CriticalError.Error")
        Private MODULELOAD_ERROR As String = Localization.GetString("ModuleLoad.Error")
        Private MODULEADD_ERROR As String = Localization.GetString("ModuleAdd.Error")
        Private CONTAINERLOAD_ERROR As String = Localization.GetString("ContainerLoad.Error")
        Private MODULELOAD_WARNING As String = Localization.GetString("ModuleLoadWarning.Error")
        Private MODULELOAD_WARNINGTEXT As String = Localization.GetString("ModuleLoadWarning.Text")

#End Region

#Region "Public Properties"

        Public Property ActionEventListeners() As ArrayList
            Get
                If _actionEventListeners Is Nothing Then
                    _actionEventListeners = New ArrayList
                End If
                Return _actionEventListeners
            End Get
            Set(ByVal Value As ArrayList)
                _actionEventListeners = Value
            End Set
        End Property

        Public ReadOnly Property SkinPath() As String
            Get
                Return Me.TemplateSourceDirectory & "/"
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Sub CollapsePane(ByVal objHtmlControl As HtmlControl)
            If Not objHtmlControl.Attributes.Item("style") Is Nothing Then
                objHtmlControl.Attributes.Remove("style")
            End If
            If Not objHtmlControl.Attributes.Item("class") Is Nothing Then
                objHtmlControl.Attributes.Item("class") = objHtmlControl.Attributes.Item("class") + " DNNEmptyPane"
            Else
                objHtmlControl.Attributes.Item("class") = "DNNEmptyPane"
            End If
        End Sub

        Private Sub CollapseUnusedPanes()
            'This method sets the width to "0" on panes that have no modules.
            'This preserves the integrity of the HTML syntax so we don't have to set
            'the visiblity of a pane to false. Setting the visibility of a pane to
            'false where there are colspans and rowspans can render the skin incorrectly.

            Dim ctlPane As Control
            Dim strPane As String
            For Each strPane In PortalSettings.ActiveTab.Panes
                ctlPane = Me.FindControl(strPane)
                Dim objHtmlControl As HtmlControl = CType(ctlPane, HtmlControl)

                'remove excess skin non-validating attributes
                objHtmlControl.Attributes.Remove("ContainerType")
                objHtmlControl.Attributes.Remove("ContainerName")
                objHtmlControl.Attributes.Remove("ContainerSrc")

                If Not ctlPane Is Nothing Then
                    If Not ctlPane.HasControls Then
                        'This pane has no controls so set the width to 0
                        CollapsePane(objHtmlControl)
                    ElseIf ctlPane.Controls.Count = 1 Then
                        'this pane has one control, check to see if it's the pane name label
                        If ctlPane.Controls(0).GetType Is GetType(Label) Then
                            'the only control in this pane is some type of label
                            If CType(ctlPane.Controls(0), Label).Text.LastIndexOf(ctlPane.ID) > 0 Then
                                'the "pane name" is the only control in this pane
                                'so, since there are no other controls, resize the pane to width="0"
                                CollapsePane(objHtmlControl)

                                'hide the pane name label
                                Dim objLabel As Label = CType(ctlPane.Controls(0), Label)
                                objLabel.Visible = False
                            End If
                        End If
                    End If
                End If


            Next
        End Sub

        Private Function LoadContainer(ByVal ContainerPath As String, ByVal objPane As Control) As UserControl
            'sanity check to ensure skin not loaded accidentally
            If ContainerPath.ToLowerInvariant.IndexOf("/skins/") <> -1 Or ContainerPath.ToLowerInvariant.IndexOf("/skins\") <> -1 Or ContainerPath.ToLowerInvariant.IndexOf("\skins\") <> -1 Or ContainerPath.ToLowerInvariant.IndexOf("\skins/") <> -1 Then
                Throw New System.Exception
            End If
            Dim ctlContainer As UserControl = Nothing

            Try
                If ContainerPath.ToLowerInvariant.IndexOf(Common.Globals.ApplicationPath.ToLowerInvariant) <> -1 Then
                    ContainerPath = ContainerPath.Remove(0, Len(Common.Globals.ApplicationPath))
                End If
                ctlContainer = CType(LoadControl("~" & ContainerPath), UserControl)
                ' call databind so that any server logic in the container is executed
                ctlContainer.DataBind()
            Catch exc As Exception
                ' could not load user control
                Dim lex As New ModuleLoadException(MODULELOAD_ERROR, exc)
                If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = True Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = True Then
                    ' only display the error to administrators
                    objPane.Controls.Add(New ErrorContainer(PortalSettings, String.Format(CONTAINERLOAD_ERROR, ContainerPath), lex).Container)
                End If
                LogException(lex)
            End Try

            Return ctlContainer
        End Function

        Private Sub ModuleMoveToPanePostBack(ByVal args As DotNetNuke.UI.Utilities.ClientAPIPostBackEventArgs)
            Dim PortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)
            If (PortalSecurity.IsInRole(PortalSettings.AdministratorRoleName.ToString) = True Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = True) Then

                Dim intModuleID As Integer = CInt(args.EventArguments("moduleid"))
                Dim strPaneName As String = CStr(args.EventArguments("pane"))
                Dim intOrder As Integer = CInt(args.EventArguments("order"))
                Dim objModules As New ModuleController

                objModules.UpdateModuleOrder(PortalSettings.ActiveTab.TabID, intModuleID, intOrder, strPaneName)
                objModules.UpdateTabModuleOrder(PortalSettings.ActiveTab.TabID, PortalSettings.ActiveTab.PortalID)

                ' Redirect to the same page to pick up changes
                Response.Redirect(Request.RawUrl, True)
            End If

        End Sub

        Private Sub ProcessActionControls(ByVal objPortalModuleBase As PortalModuleBase, ByVal objContainer As Control)

            Dim objActions As Containers.ActionBase

            For Each objChildControl As Control In objContainer.Controls
                ' check if control is an action control
                If TypeOf objChildControl Is Containers.ActionBase Then
                    objActions = CType(objChildControl, Containers.ActionBase)
                    objActions.PortalModule = objPortalModuleBase
                    objActions.MenuActions.AddRange(objPortalModuleBase.Actions)
                    AddHandler objActions.Action, AddressOf ModuleAction_Click
                End If

                If objChildControl.HasControls Then
                    ' recursive call for child controls
                    ProcessActionControls(objPortalModuleBase, objChildControl)
                End If
            Next

        End Sub

#End Region

#Region "Public Methods"

        Public Sub InjectModule(ByVal objPane As Control, ByVal objModule As ModuleInfo, ByVal PortalSettings As PortalSettings)

            Dim bSuccess As Boolean = True

            Try
                'Get a reference to the Page
                Dim DefaultPage As CDefault = DirectCast(Page, CDefault)
                Dim objPortalModuleBase As Entities.Modules.PortalModuleBase = Nothing

                ' load container control
                Dim ctlContainer As UserControl = Nothing

                Dim objSkins As New UI.Skins.SkinController

                'Save the current ContainerSrc/Path (in case we are in "Preview" mode)
                Dim strOldContainerSource As String = objModule.ContainerSrc
                Dim strOldContainerPath As String = objModule.ContainerPath

                ' container preview
                Dim PreviewModuleId As Integer = -1
                If Not Request.QueryString("ModuleId") Is Nothing Then
                    PreviewModuleId = Integer.Parse(Request.QueryString("ModuleId"))
                End If
                If (Not Request.QueryString("ContainerSrc") Is Nothing) And (objModule.ModuleID = PreviewModuleId Or PreviewModuleId = -1) Then
                    objModule.ContainerSrc = SkinController.FormatSkinSrc(QueryStringDecode(Request.QueryString("ContainerSrc")) & ".ascx", PortalSettings)
                    ctlContainer = LoadContainer(objModule.ContainerSrc, objPane)
                End If

                ' load user container ( based on cookie )
                If ctlContainer Is Nothing Then
                    If Not Request.Cookies("_ContainerSrc" & PortalSettings.PortalId.ToString) Is Nothing Then
                        If Request.Cookies("_ContainerSrc" & PortalSettings.PortalId.ToString).Value <> "" Then
                            objModule.ContainerSrc = SkinController.FormatSkinSrc(Request.Cookies("_ContainerSrc" & PortalSettings.PortalId.ToString).Value & ".ascx", PortalSettings)
                            ctlContainer = LoadContainer(objModule.ContainerSrc, objPane)
                        End If
                    End If
                End If

                If ctlContainer Is Nothing Then
                    ' if the module specifies that no container should be used
                    If objModule.DisplayTitle = False Then
                        ' always display container if the current user is the administrator or the module is being used in an admin case
                        Dim blnDisplayTitle As Boolean = (PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString)) Or IsAdminSkin(PortalSettings.ActiveTab.IsAdminTab)
                        ' unless the administrator is in view mode
                        If blnDisplayTitle = True Then
                            blnDisplayTitle = (PortalSettings.UserMode <> PortalSettings.Mode.View)
                        End If
                        If blnDisplayTitle = False Then
                            objModule.ContainerSrc = SkinController.FormatSkinSrc("[G]" & SkinInfo.RootContainer & "/_default/No Container.ascx", PortalSettings)
                            ctlContainer = LoadContainer(objModule.ContainerSrc, objPane)
                        End If
                    End If
                End If

                If ctlContainer Is Nothing Then
                    ' if this is not a container assigned to a module
                    If objModule.ContainerSrc = PortalSettings.ActiveTab.ContainerSrc Then
                        ' look for a container specification in the skin pane
                        If TypeOf objPane Is HtmlControl Then
                            Dim objHtmlControl As HtmlControl = CType(objPane, HtmlControl)
                            If (Not objHtmlControl.Attributes("ContainerSrc") Is Nothing) Then
                                Dim validSrc As Boolean = False
                                If (Not objHtmlControl.Attributes("ContainerType") Is Nothing) And (Not objHtmlControl.Attributes("ContainerName") Is Nothing) Then
                                    ' legacy container specification in skin pane
                                    objModule.ContainerSrc = "[" & objHtmlControl.Attributes("ContainerType") & "]" & SkinInfo.RootContainer & "/" & objHtmlControl.Attributes("ContainerName") & "/" & objHtmlControl.Attributes("ContainerSrc")
                                    validSrc = True
                                Else
                                    ' 3.0 container specification in skin pane
                                    objModule.ContainerSrc = objHtmlControl.Attributes("ContainerSrc")

                                    ' The ContainerSrc should contain both a directory and filename 
                                    ' i.e. "DNN-Blue/Text Header - Color Background.ascx"
                                    If objModule.ContainerSrc.Contains("/") Then
                                        ' If container type (global or local) is not specified, then use the type from the current skin
                                        If Not (objModule.ContainerSrc.ToLowerInvariant.StartsWith("[g]") OrElse _
                                            objModule.ContainerSrc.ToLowerInvariant.StartsWith("[l]")) Then

                                            ' This assumes that ActiveTab.SkinSrc has a valid skin path
                                            If SkinController.IsGlobalSkin(PortalSettings.ActiveTab.SkinSrc) Then
                                                objModule.ContainerSrc = String.Format("[G]containers/{0}", objModule.ContainerSrc.TrimStart("/"c))
                                            Else
                                                objModule.ContainerSrc = String.Format("[L]containers/{0}", objModule.ContainerSrc.TrimStart("/"c))
                                            End If
                                            validSrc = True
                                        End If
                                    End If
                                End If
                                If validSrc Then
                                    objModule.ContainerSrc = SkinController.FormatSkinSrc(objModule.ContainerSrc, PortalSettings)
                                    ctlContainer = LoadContainer(objModule.ContainerSrc, objPane)
                                End If
                            End If
                        End If
                    End If
                End If

                ' else load assigned container
                If ctlContainer Is Nothing Then
                    If IsAdminSkin(PortalSettings.ActiveTab.IsAdminTab) Then
                        Dim objSkin As SkinInfo = SkinController.GetSkin(SkinInfo.RootContainer, PortalSettings.PortalId, SkinType.Admin)
                        If Not objSkin Is Nothing Then
                            objModule.ContainerSrc = objSkin.SkinSrc
                        Else
                            objModule.ContainerSrc = ""
                        End If
                    End If

                    If objModule.ContainerSrc <> "" Then
                        objModule.ContainerSrc = SkinController.FormatSkinSrc(objModule.ContainerSrc, PortalSettings)
                        ctlContainer = LoadContainer(objModule.ContainerSrc, objPane)
                    End If
                End If

                ' error loading container - load default
                If ctlContainer Is Nothing Then
                    If IsAdminSkin(PortalSettings.ActiveTab.IsAdminTab) Then
                        objModule.ContainerSrc = Common.Globals.HostPath & SkinInfo.RootContainer & DefaultContainer.Folder & DefaultContainer.AdminDefaultName
                    Else
                        objModule.ContainerSrc = Common.Globals.HostPath & SkinInfo.RootContainer & DefaultContainer.Folder & DefaultContainer.DefaultName
                    End If
                    ctlContainer = LoadContainer(objModule.ContainerSrc, objPane)
                End If

                ' set container path
                objModule.ContainerPath = SkinController.FormatSkinPath(objModule.ContainerSrc)

                Dim ID As String
                Dim objCSSCache As Hashtable = Nothing
                If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                    objCSSCache = CType(DataCache.GetCache("CSS"), Hashtable)
                End If
                If objCSSCache Is Nothing Then
                    objCSSCache = New Hashtable
                End If

                ' container package style sheet
                ID = CreateValidID(objModule.ContainerPath)
                If objCSSCache.ContainsKey(ID) = False Then
                    If File.Exists(Server.MapPath(objModule.ContainerPath) & "container.css") Then
                        objCSSCache(ID) = objModule.ContainerPath & "container.css"
                    Else
                        objCSSCache(ID) = ""
                    End If
                    If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                        DataCache.SetCache("CSS", objCSSCache)
                    End If
                End If
                If objCSSCache(ID).ToString <> "" Then
                    DefaultPage.AddStyleSheet(ID, objCSSCache(ID).ToString)
                End If

                ' container file style sheet
                ID = CreateValidID(objModule.ContainerSrc.Replace(".ascx", ".css"))
                If objCSSCache.ContainsKey(ID) = False Then
                    If File.Exists(Server.MapPath(Replace(objModule.ContainerSrc, ".ascx", ".css"))) Then
                        objCSSCache(ID) = Replace(objModule.ContainerSrc, ".ascx", ".css")
                    Else
                        objCSSCache(ID) = ""
                    End If
                    If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                        DataCache.SetCache("CSS", objCSSCache)
                    End If
                End If
                If objCSSCache(ID).ToString <> "" Then
                    DefaultPage.AddStyleSheet(ID, objCSSCache(ID).ToString)
                End If

                If Not IsAdminControl() Then
                    ' inject an anchor tag to allow navigation to the module content
                    objPane.Controls.Add(New LiteralControl("<a name=""" & objModule.ModuleID.ToString & """></a>"))
                End If

                ' get container pane
                Dim objCell As Control = ctlContainer.FindControl(glbDefaultPane)
                Dim EditText As String = ""
                Dim DisplayOptions As Boolean = False

                If Not objCell Is Nothing Then
                    ' set container content pane display properties ( could be <TD>,<DIV>,<SPAN>,<P> )
                    If TypeOf objCell Is HtmlContainerControl Then
                        Dim cell As HtmlContainerControl = CType(objCell, HtmlContainerControl)

                        If objModule.Alignment <> "" Then
                            If Not cell.Attributes.Item("class") Is Nothing Then
                                cell.Attributes.Item("class") = cell.Attributes.Item("class") + " DNNAlign" + objModule.Alignment.ToLowerInvariant()
                            Else
                                cell.Attributes.Item("class") = "DNNAlign" + objModule.Alignment.ToLowerInvariant()
                            End If
                        End If
                        If objModule.Color <> "" Then
                            cell.Style("background-color") = objModule.Color
                        End If
                        If objModule.Border <> "" Then
                            cell.Style("border-top") = objModule.Border & "px #000000 solid"
                            cell.Style("border-bottom") = objModule.Border & "px #000000 solid"
                            cell.Style("border-right") = objModule.Border & "px #000000 solid"
                            cell.Style("border-left") = objModule.Border & "px #000000 solid"
                        End If

                        ' display visual indicator if module is only visible to administrators
                        If IsAdminControl() = False And PortalSettings.ActiveTab.IsAdminTab = False Then
                            If (objModule.StartDate >= Now Or objModule.EndDate <= Now) Or (objModule.AuthorizedViewRoles.ToLowerInvariant = ";" & PortalSettings.AdministratorRoleName.ToLowerInvariant & ";") Then
                                cell.Style("border-top") = "2px #FF0000 solid"
                                cell.Style("border-bottom") = "2px #FF0000 solid"
                                cell.Style("border-right") = "2px #FF0000 solid"
                                cell.Style("border-left") = "2px #FF0000 solid"
                                objCell.Controls.Add(New LiteralControl("<span class=""NormalRed""><center>" & Localization.GetString("ModuleVisibleAdministrator.Text") & "</center></span>"))
                            End If
                        End If
                    End If

                    If Not IsAdminControl() Then
                        ' inject a start comment around the module content
                        objCell.Controls.Add(New LiteralControl("<!-- Start_Module_" & objModule.ModuleID.ToString & " -->"))
                    End If

                    ' inject the header
                    If objModule.Header <> "" Then
                        Dim objLabel As New Label
                        objLabel.Text = objModule.Header
                        objLabel.CssClass = "Normal"
                        objCell.Controls.Add(objLabel)
                    End If

                    ' create a wrapper panel control for the module content min/max
                    Dim objPanel As New Panel
                    objPanel.ID = "ModuleContent"

                    ' module content visibility options
                    Dim blnContent As Boolean = PortalSettings.ContentVisible
                    If Not Request.QueryString("content") Is Nothing Then
                        Select Case Request.QueryString("Content").ToLowerInvariant
                            Case "1", "true"
                                blnContent = True
                            Case "0", "false"
                                blnContent = False
                        End Select
                    End If
                    If IsAdminControl() = True Or PortalSettings.ActiveTab.IsAdminTab Then
                        blnContent = True
                    End If

                    ' check if user has EDIT permissions for module
                    Dim blnHasModuleEditPermissions As Boolean = PortalSecurity.HasNecessaryPermission(SecurityAccessLevel.Edit, PortalSettings, objModule, UserController.GetCurrentUserInfo.Username)
                    If blnHasModuleEditPermissions = True AndAlso objModule.DefaultCacheTime <> -1 Then
                        m_blnHasModuleEditPermissions = True
                    End If

                    Dim blnDynamic As Boolean = False

                    ' try to load the module user control
                    Try
                        If blnContent Then
                            ' if the module supports caching and caching is enabled for the instance and the user does not have Edit rights or is currently in View mode
                            If objModule.DefaultCacheTime <> -1 AndAlso objModule.CacheTime <> 0 AndAlso (PortalSecurity.HasNecessaryPermission(SecurityAccessLevel.Edit, PortalSettings, objModule, UserController.GetCurrentUserInfo.Username) = False Or PortalSettings.UserMode = PortalSettings.Mode.View) Then
                                ' use output caching
                                objPortalModuleBase = New PortalModuleBase
                            Else
                                ' load the control dynamically
                                blnDynamic = True
                                If objModule.ControlSrc.ToLowerInvariant.EndsWith(".ascx") Then
                                    ' load from a user control on the file system
                                    objPortalModuleBase = CType(Me.LoadControl("~/" & objModule.ControlSrc), PortalModuleBase)
                                Else
                                    ' load from a typename in an assembly ( ie. server control )
                                    Dim objType As System.Type = Framework.Reflection.CreateType(objModule.ControlSrc)
                                    objPortalModuleBase = CType(Me.LoadControl(objType, Nothing), PortalModuleBase)
                                End If
                            End If

                            ' set the control ID to the resource file name ( ie. controlname.ascx = controlname )
                            ' this is necessary for the Localization in PageBase
                            objPortalModuleBase.ID = Path.GetFileNameWithoutExtension(objModule.ControlSrc)

                        Else       ' content placeholder
                            objPortalModuleBase = CType(Me.LoadControl("~/admin/Portal/NoContent.ascx"), PortalModuleBase)
                        End If

                        'check for IMC
                        objCommunicator.LoadCommunicator(objPortalModuleBase)

                        ' add module settings
                        objPortalModuleBase.ModuleConfiguration = objModule

                    Catch exc As Threading.ThreadAbortException
                        Threading.Thread.ResetAbort()
                        bSuccess = False
                    Catch exc As Exception
                        bSuccess = False
                        objPortalModuleBase = CType(Me.LoadControl("~/admin/Portal/NoContent.ascx"), PortalModuleBase)

                        '' add module settings
                        objPortalModuleBase.ModuleConfiguration = objModule

                        If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = True Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = True Then
                            ' only display the error to administrators
                            If objPortalModuleBase Is Nothing Then
                                ProcessModuleLoadException(MODULELOAD_ERROR, objPanel, exc)
                            Else
                                ProcessModuleLoadException(objPortalModuleBase, exc)
                            End If
                        End If
                    End Try

                    ' module user control processing
                    If Not objPortalModuleBase Is Nothing Then
                        'if module is dynamically loaded and AJAX is installed and the control supports partial rendering (defined in ModuleControls table )
                        If blnDynamic = True AndAlso objModule.SupportsPartialRendering = True AndAlso AJAX.IsInstalled = True Then
                            'register AJAX
                            AJAX.RegisterScriptManager()
                            'enable Partial Rendering
                            AJAX.SetScriptManagerProperty(Me.Page, "EnablePartialRendering", New Object() {True})
                            'create update panel
                            Dim objUpdatePanel As Control = AJAX.CreateUpdatePanelControl
                            objUpdatePanel.ID = objPortalModuleBase.ID & "_UP"
                            'get update panel content template
                            Dim objContentTemplateContainer As Control = AJAX.ContentTemplateContainerControl(objUpdatePanel)
                            ' inject a message placeholder for common module messaging - UI.Skins.Skin.AddModuleMessage
                            Dim MessagePlaceholder As New PlaceHolder
                            MessagePlaceholder.ID = "MessagePlaceHolder"
                            MessagePlaceholder.Visible = False
                            objContentTemplateContainer.Controls.Add(MessagePlaceholder)
                            'inject module into update panel content template
                            objContentTemplateContainer.Controls.Add(objPortalModuleBase)
                            'inject the update panel into the panel
                            objPanel.Controls.Add(objUpdatePanel)
                            'create image for update progress control
                            Dim objImage As System.Web.UI.WebControls.Image = New System.Web.UI.WebControls.Image()
                            objImage.ImageUrl = "~/images/progressbar.gif"  'hardcoded
                            'inject updateprogress into the panel
                            objPanel.Controls.Add(AJAX.CreateUpdateProgressControl(objUpdatePanel.ID, objImage))
                        Else
                            ' inject a message placeholder for common module messaging - UI.Skins.Skin.AddModuleMessage
                            Dim MessagePlaceholder As New PlaceHolder
                            MessagePlaceholder.ID = "MessagePlaceHolder"
                            MessagePlaceholder.Visible = False
                            objPanel.Controls.Add(MessagePlaceholder)
                            'inject the module into the panel
                            objPanel.Controls.Add(objPortalModuleBase)
                        End If
                    End If

                    ' inject the panel into the container pane
                    objCell.Controls.Add(objPanel)

                    ' disable legacy controls in module 
                    If Not objPortalModuleBase Is Nothing Then

                        ' force the CreateChildControls() event to fire for the PortalModuleBase ( the timing is critical for output caching )
                        objPortalModuleBase.FindControl("")

                        Dim objModuleContent As Panel = CType(objPortalModuleBase.FindControl("ModuleContent"), Panel)
                        If Not objModuleContent Is Nothing Then
                            objModuleContent.Visible = False
                        End If
                    End If

                    ' inject the footer
                    If objModule.Footer <> "" Then
                        Dim objLabel As New Label
                        objLabel.Text = objModule.Footer
                        objLabel.CssClass = "Normal"
                        objCell.Controls.Add(objLabel)
                    End If

                    ' inject an end comment around the module content
                    If Not IsAdminControl() Then
                        objPanel.Controls.Add(New LiteralControl("<!-- End_Module_" & objModule.ModuleID.ToString & " -->"))
                    End If

                End If

                ' set container id to an explicit short name to reduce page payload 
                ctlContainer.ID = "ctr"
                ' make the container id unique for the page
                If Not objPortalModuleBase Is Nothing AndAlso objPortalModuleBase.ModuleId > -1 Then                'Can't have ID with a - (dash) in it, should only be for admin modules, where they are the only container, so don't need unique name
                    ctlContainer.ID += objPortalModuleBase.ModuleId.ToString
                End If

                If Not objPortalModuleBase Is Nothing AndAlso Common.Globals.IsLayoutMode AndAlso Common.Globals.IsAdminControl() = False Then
                    'provide Drag-N-Drop capabilities
                    Dim ctlDragDropContainer As Panel = New Panel
                    Dim ctlTitle As System.Web.UI.Control = ctlContainer.FindControl("dnnTitle")                      ''Assume that the title control is named dnnTitle.  If this becomes an issue we could loop through the controls looking for the title type of skin object
                    ctlDragDropContainer.ID = ctlContainer.ID & "_DD"
                    objPane.Controls.Add(ctlDragDropContainer)

                    ' inject the container into the page pane - this triggers the Pre_Init() event for the user control
                    ctlDragDropContainer.Controls.Add(ctlContainer)

                    If Not ctlTitle Is Nothing Then
                        If ctlTitle.Controls.Count > 0 Then
                            ' if multiple title controls, use the first instance
                            ctlTitle = ctlTitle.Controls(0)
                        End If
                    End If

                    ' enable drag and drop
                    If Not ctlDragDropContainer Is Nothing AndAlso Not ctlTitle Is Nothing Then                   'The title ID is actually the first child so we need to make sure at least one child exists
                        UI.Utilities.DNNClientAPI.EnableContainerDragAndDrop(ctlTitle, ctlDragDropContainer, objPortalModuleBase.ModuleId)
                        DotNetNuke.UI.Utilities.ClientAPI.RegisterPostBackEventHandler(objCell, "MoveToPane", AddressOf ModuleMoveToPanePostBack, False)
                    End If
                Else
                    ' inject the container into the page pane - this triggers the Pre_Init() event for the user control
                    objPane.Controls.Add(ctlContainer)
                End If

                'Process the Action Controls
                ProcessActionControls(objPortalModuleBase, ctlContainer)

                ' process the base class module properties 
                If Not objPortalModuleBase Is Nothing Then
                    Dim controlSrc As String = objModule.ControlSrc
                    Dim folderName As String = objModule.FolderName

                    ' module stylesheet
                    ID = CreateValidID(Common.Globals.ApplicationPath & "/DesktopModules/" & folderName)
                    If objCSSCache.ContainsKey(ID) = False Then
                        ' default to nothing
                        objCSSCache(ID) = ""

                        ' 1.try to load module.css from module folder
                        If File.Exists(Server.MapPath(Common.Globals.ApplicationPath & "/DesktopModules/" & folderName & "/module.css")) Then
                            objCSSCache(ID) = Common.Globals.ApplicationPath & "/DesktopModules/" & folderName & "/module.css"
                        Else
                            ' 2.otherwise try to load from Path to control
                            If controlSrc.ToLowerInvariant.EndsWith(".ascx") Then
                                If File.Exists(Server.MapPath(Common.Globals.ApplicationPath & "/" & controlSrc.Substring(0, controlSrc.LastIndexOf("/") + 1)) & "module.css") Then
                                    objCSSCache(ID) = Common.Globals.ApplicationPath & "/" & controlSrc.Substring(0, controlSrc.LastIndexOf("/") + 1) & "module.css"
                                End If
                            End If
                        End If
                    End If

                    If objCSSCache.ContainsKey(ID) AndAlso Not String.IsNullOrEmpty(objCSSCache(ID).ToString()) Then
                        'Add it to beginning of style list
                        DefaultPage.AddStyleSheet(ID, objCSSCache(ID).ToString(), True)
                    End If
                End If

                ' display collapsible page panes
                If objPane.Visible = False Then
                    objPane.Visible = True
                End If

                'Reset the ContainerSource/Path (in case we are in "Preview" mode)
                objModule.ContainerPath = strOldContainerPath
                objModule.ContainerSrc = strOldContainerSource

            Catch exc As Exception
                Dim lex As ModuleLoadException
                lex = New ModuleLoadException(String.Format(MODULEADD_ERROR, objPane.ID.ToString), exc)
                If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = True Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = True Then
                    ' only display the error to administrators
                    objPane.Controls.Add(New ErrorContainer(PortalSettings, MODULELOAD_ERROR, lex).Container)
                End If
                LogException(exc)
                bSuccess = False
                Throw lex
            End Try

            If Not bSuccess Then
                Throw New ModuleLoadException()
            End If
        End Sub

        Public Sub RegisterModuleActionEvent(ByVal ModuleID As Integer, ByVal e As ActionEventHandler)
            ActionEventListeners.Add(New ModuleActionEventListener(ModuleID, e))
        End Sub

#End Region

#Region "Public Shared/Static methods"

        Public Shared Sub AddModuleMessage(ByVal objPortalModuleBase As Entities.Modules.PortalModuleBase, ByVal Message As String, ByVal objModuleMessageType As UI.Skins.Controls.ModuleMessage.ModuleMessageType)
            AddModuleMessage(objPortalModuleBase, "", Message, objModuleMessageType)
        End Sub

        Public Shared Sub AddModuleMessage(ByVal objPortalModuleBase As Entities.Modules.PortalModuleBase, ByVal Heading As String, ByVal Message As String, ByVal objModuleMessageType As UI.Skins.Controls.ModuleMessage.ModuleMessageType)
            If Not objPortalModuleBase Is Nothing Then
                If Message <> "" Then
                    Dim MessagePlaceHolder As PlaceHolder = CType(objPortalModuleBase.Parent.FindControl("MessagePlaceHolder"), PlaceHolder)
                    If Not MessagePlaceHolder Is Nothing Then
                        MessagePlaceHolder.Visible = True
                        Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
                        objModuleMessage = GetModuleMessageControl(Heading, Message, objModuleMessageType)
                        MessagePlaceHolder.Controls.Add(objModuleMessage)
                        'CType(objPortalModuleBase.Page, CDefault).ScrollToControl(MessagePlaceHolder.Parent)       'scroll to error message
                    End If
                End If
            End If
        End Sub

        Public Shared Sub AddModuleMessage(ByVal objPortalModuleBase As Entities.Modules.PortalModuleBase, ByVal Heading As String, ByVal Message As String, ByVal IconSrc As String)
            If Not objPortalModuleBase Is Nothing Then
                If Message <> "" Then
                    Dim MessagePlaceHolder As PlaceHolder = CType(objPortalModuleBase.Parent.FindControl("MessagePlaceHolder"), PlaceHolder)
                    If Not MessagePlaceHolder Is Nothing Then
                        MessagePlaceHolder.Visible = True
                        Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
                        objModuleMessage = GetModuleMessageControl(Heading, Message, IconSrc)
                        MessagePlaceHolder.Controls.Add(objModuleMessage)
                    End If
                End If
            End If
        End Sub

        Public Shared Sub AddPageMessage(ByVal objPage As Page, ByVal Heading As String, ByVal Message As String, ByVal IconSrc As String)

            If Message <> "" Then
                Dim ContentPane As Control = CType(objPage.FindControl(glbDefaultPane), Control)
                If Not ContentPane Is Nothing Then
                    Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
                    objModuleMessage = GetModuleMessageControl(Heading, Message, IconSrc)
                    ContentPane.Controls.AddAt(0, objModuleMessage)
                End If
            End If
        End Sub

        Public Shared Sub AddPageMessage(ByVal objSkin As UI.Skins.Skin, ByVal Heading As String, ByVal Message As String, ByVal IconSrc As String)

            If Message <> "" Then
                Dim ContentPane As Control = CType(objSkin.FindControl(glbDefaultPane), Control)
                If Not ContentPane Is Nothing Then
                    Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
                    objModuleMessage = GetModuleMessageControl(Heading, Message, IconSrc)
                    ContentPane.Controls.AddAt(0, objModuleMessage)
                End If
            End If

        End Sub

        Public Shared Sub AddPageMessage(ByVal objSkin As UI.Skins.Skin, ByVal Heading As String, ByVal Message As String, ByVal objModuleMessageType As UI.Skins.Controls.ModuleMessage.ModuleMessageType)

            If Message <> "" Then
                Dim ContentPane As Control = CType(objSkin.FindControl(glbDefaultPane), Control)
                If Not ContentPane Is Nothing Then
                    Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
                    objModuleMessage = GetModuleMessageControl(Heading, Message, objModuleMessageType)
                    ContentPane.Controls.AddAt(0, objModuleMessage)
                End If
            End If

        End Sub

        Public Shared Sub AddPageMessage(ByVal objPage As Page, ByVal Heading As String, ByVal Message As String, ByVal objModuleMessageType As UI.Skins.Controls.ModuleMessage.ModuleMessageType)

            If Message <> "" Then
                Dim ContentPane As Control = CType(objPage.FindControl(glbDefaultPane), Control)
                If Not ContentPane Is Nothing Then
                    Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
                    objModuleMessage = GetModuleMessageControl(Heading, Message, objModuleMessageType)
                    ContentPane.Controls.AddAt(0, objModuleMessage)
                End If
            End If
        End Sub

        Public Shared Function GetModuleMessageControl(ByVal Heading As String, ByVal Message As String, ByVal IconImage As String) As UI.Skins.Controls.ModuleMessage
            'Use this to get a module message control
            'with a custom image for an icon
            Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
            Dim s As New UI.Skins.Skin
            objModuleMessage = CType(s.LoadControl("~/admin/skins/ModuleMessage.ascx"), UI.Skins.Controls.ModuleMessage)
            objModuleMessage.Heading = Heading
            objModuleMessage.Text = Message
            objModuleMessage.IconImage = IconImage
            Return objModuleMessage
        End Function

        Public Shared Function GetModuleMessageControl(ByVal Heading As String, ByVal Message As String, ByVal objModuleMessageType As UI.Skins.Controls.ModuleMessage.ModuleMessageType) As UI.Skins.Controls.ModuleMessage
            'Use this to get a module message control
            'with a standard DotNetNuke icon
            Dim objModuleMessage As UI.Skins.Controls.ModuleMessage
            Dim s As New UI.Skins.Skin
            objModuleMessage = CType(s.LoadControl("~/admin/skins/ModuleMessage.ascx"), UI.Skins.Controls.ModuleMessage)
            objModuleMessage.Heading = Heading
            objModuleMessage.Text = Message
            objModuleMessage.IconType = objModuleMessageType
            Return objModuleMessage
        End Function

        Public Shared Function GetParentSkin(ByVal objModule As Entities.Modules.PortalModuleBase) As UI.Skins.Skin
            Dim MyParent As System.Web.UI.Control = objModule.Parent
            Dim FoundSkin As Boolean = False
            While Not MyParent Is Nothing
                If TypeOf MyParent Is Skin Then
                    FoundSkin = True
                    Exit While
                End If
                MyParent = MyParent.Parent
            End While
            If FoundSkin Then
                Return DirectCast(MyParent, Skin)
            Else
                Return Nothing
            End If
        End Function

#End Region

#Region "Event Handlers"

        Private Sub Page_Init(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Init
            '
            ' CODEGEN: This call is required by the ASP.NET Web Form Designer.
            '
            InitializeComponent()

            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo = Nothing
            Dim ctlPane As Control
            Dim blnLayoutMode As Boolean = Common.Globals.IsLayoutMode

            Dim bSuccess As Boolean = True

            ' iterate page controls
            Dim ctlControl As Control
            Dim objHtmlControl As HtmlControl
            For Each ctlControl In Me.Controls
                ' load the skin panes
                If TypeOf ctlControl Is HtmlControl Then
                    objHtmlControl = CType(ctlControl, HtmlControl)
                    If Not objHtmlControl.ID Is Nothing Then
                        Select Case objHtmlControl.TagName.ToLowerInvariant
                            Case "td", "div", "span", "p"
                                ' content pane
                                If ctlControl.ID.ToLowerInvariant() <> "controlpanel" Then
                                    PortalSettings.ActiveTab.Panes.Add(ctlControl.ID)
                                End If
                        End Select
                    End If
                End If
            Next

            If Not IsAdminControl() Then    ' master module

                If PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AuthorizedRoles) Then

                    ' check portal expiry date
                    Dim blnExpired As Boolean = False
                    If PortalSettings.ExpiryDate <> Null.NullDate Then
                        If Convert.ToDateTime(PortalSettings.ExpiryDate) < Now() And PortalSettings.ActiveTab.ParentId <> PortalSettings.AdminTabId And PortalSettings.ActiveTab.ParentId <> PortalSettings.SuperTabId Then
                            blnExpired = True
                        End If
                    End If
                    If Not blnExpired Then
                        If (PortalSettings.ActiveTab.StartDate < Now And PortalSettings.ActiveTab.EndDate > Now) Or blnLayoutMode = True Then
                            ' process panes
                            If blnLayoutMode Then
                                Dim strPane As String
                                For Each strPane In PortalSettings.ActiveTab.Panes
                                    ctlPane = Me.FindControl(strPane)
                                    ctlPane.Visible = True

                                    ' display pane border
                                    If TypeOf ctlPane Is HtmlContainerControl Then
                                        CType(ctlPane, HtmlContainerControl).Style("border-top") = "1px #CCCCCC dotted"
                                        CType(ctlPane, HtmlContainerControl).Style("border-bottom") = "1px #CCCCCC dotted"
                                        CType(ctlPane, HtmlContainerControl).Style("border-right") = "1px #CCCCCC dotted"
                                        CType(ctlPane, HtmlContainerControl).Style("border-left") = "1px #CCCCCC dotted"
                                    End If

                                    ' display pane name
                                    Dim ctlLabel As New Label
                                    ctlLabel.Text = "<center>" & strPane & "</center><br>"
                                    ctlLabel.CssClass = "SubHead"
                                    ctlPane.Controls.AddAt(0, ctlLabel)
                                Next
                            End If

                            ' dynamically populate the panes with modules
                            If PortalSettings.ActiveTab.Modules.Count > 0 Then

                                ' loop through each entry in the configuration system for this tab
                                For Each objModule In PortalSettings.ActiveTab.Modules

                                    ' if user is allowed to view module and module is not deleted
                                    If PortalSecurity.IsInRoles(objModule.AuthorizedViewRoles) = True And objModule.IsDeleted = False Then

                                        ' if current date is within module display schedule or user is admin
                                        If (objModule.StartDate < Now And objModule.EndDate > Now) Or blnLayoutMode = True Then

                                            ' modules which are displayed on all tabs should not be displayed on the Admin or Super tabs
                                            If objModule.AllTabs = False Or PortalSettings.ActiveTab.IsAdminTab = False Then

                                                Dim parent As Control = Me.FindControl(objModule.PaneName)

                                                If parent Is Nothing Then
                                                    ' the pane specified in the database does not exist for this skin
                                                    ' insert the module into the default pane instead
                                                    parent = Me.FindControl(glbDefaultPane)
                                                End If

                                                If Not parent Is Nothing Then
                                                    ' try to localize admin modules
                                                    If PortalSettings.ActiveTab.IsAdminTab Then
                                                        objModule.ModuleTitle = Localization.LocalizeControlTitle(objModule.ModuleTitle, objModule.ControlSrc, "")
                                                    End If

                                                    'try to inject the module into the skin
                                                    Try
                                                        InjectModule(parent, objModule, PortalSettings)
                                                    Catch ex As Exception
                                                        bSuccess = False
                                                    End Try
                                                Else             ' no ContentPane in skin
                                                    Dim lex As ModuleLoadException
                                                    lex = New ModuleLoadException(PANE_LOAD_ERROR)
                                                    Controls.Add(New ErrorContainer(PortalSettings, MODULELOAD_ERROR, lex).Container)
                                                    LogException(lex)
                                                    Err.Clear()
                                                End If

                                            End If

                                        End If

                                    End If
                                Next objModule
                            End If
                        Else
                            Skin.AddPageMessage(Me, "", TABACCESS_ERROR, UI.Skins.Controls.ModuleMessage.ModuleMessageType.YellowWarning)
                        End If
                    Else
                        Skin.AddPageMessage(Me, "", String.Format(CONTRACTEXPIRED_ERROR, PortalSettings.PortalName, GetMediumDate(PortalSettings.ExpiryDate.ToString), PortalSettings.Email), UI.Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
                    End If
                Else
                    Response.Redirect(AccessDeniedURL(TABACCESS_ERROR), True)
                End If

            Else    ' slave module
                Dim ModuleId As Integer = -1
                Dim Key As String = ""
                Dim slaveModule As ModuleInfo = Nothing

                ' get ModuleId
                If Not IsNothing(Request.QueryString("mid")) Then
                    ModuleId = Int32.Parse(Request.QueryString("mid"))
                End If

                ' get ControlKey
                If Not IsNothing(Request.QueryString("ctl")) Then
                    Key = Request.QueryString("ctl")
                End If

                ' initialize moduleid for modulesettings
                If Not IsNothing(Request.QueryString("moduleid")) And (Key.ToLowerInvariant = "module" Or Key.ToLowerInvariant = "help") Then
                    ModuleId = Int32.Parse(Request.QueryString("moduleid"))
                End If

                If ModuleId <> -1 Then
                    ' get master module security settings
                    objModule = objModules.GetModule(ModuleId, PortalSettings.ActiveTab.TabID, False)
                    If Not objModule Is Nothing Then
                        'Clone the Master Module as we do not want to modify the cached module
                        slaveModule = objModule.Clone()
                        If slaveModule.InheritViewPermissions Then
                            slaveModule.AuthorizedViewRoles = PortalSettings.ActiveTab.AuthorizedRoles
                        End If
                    End If
                End If

                If slaveModule Is Nothing Then
                    ' initialize object not related to a module
                    slaveModule = New ModuleInfo
                    slaveModule.ModuleID = ModuleId
                    slaveModule.ModuleDefID = -1
                    slaveModule.TabID = PortalSettings.ActiveTab.TabID
                    slaveModule.AuthorizedEditRoles = ""
                    slaveModule.AuthorizedViewRoles = ""
                    Dim objModulePermissionController As New ModulePermissionController
                    slaveModule.ModulePermissions = objModulePermissionController.GetModulePermissionsCollectionByModuleID(slaveModule.ModuleID, slaveModule.TabID)
                End If

                ' initialize moduledefid for modulesettings
                If Not IsNothing(Request.QueryString("moduleid")) And (Key.ToLowerInvariant = "module" Or Key.ToLowerInvariant = "help") Then
                    slaveModule.ModuleDefID = -1
                End If

                ' override slave module settings
                If Request.QueryString("dnnprintmode") <> "true" Then
                    slaveModule.ModuleTitle = ""
                End If
                slaveModule.Header = ""
                slaveModule.Footer = ""
                slaveModule.StartDate = DateTime.MinValue
                slaveModule.EndDate = DateTime.MaxValue
                slaveModule.PaneName = glbDefaultPane
                slaveModule.Visibility = VisibilityState.None
                slaveModule.Color = ""
                If Request.QueryString("dnnprintmode") <> "true" Then
                    slaveModule.Alignment = "center"
                End If
                slaveModule.Border = ""
                slaveModule.DefaultCacheTime = -1
                slaveModule.DisplayTitle = True
                slaveModule.DisplayPrint = False
                slaveModule.DisplaySyndicate = False

                ' get container from Active Tab for slave module
                If Not String.IsNullOrEmpty(PortalSettings.ActiveTab.ContainerSrc) Then
                    slaveModule.ContainerSrc = PortalSettings.ActiveTab.ContainerSrc
                End If
                If String.IsNullOrEmpty(slaveModule.ContainerSrc) Then
                    'Next try default container for portal
                    If Not PortalSettings.PortalContainer Is Nothing Then
                        slaveModule.ContainerSrc = PortalSettings.PortalContainer.SkinSrc
                    End If
                    'Finally try default container for app
                    If String.IsNullOrEmpty(slaveModule.ContainerSrc) Then
                        slaveModule.ContainerSrc = "[G]" & SkinInfo.RootContainer & DefaultContainer.Folder & DefaultContainer.DefaultName
                    End If
                    slaveModule.ContainerSrc = SkinController.FormatSkinSrc(slaveModule.ContainerSrc, PortalSettings)
                End If
                slaveModule.ContainerPath = SkinController.FormatSkinPath(slaveModule.ContainerSrc)

                ' get the pane
                Dim parent As Control = Me.FindControl(slaveModule.PaneName)

                ' load the controls
                Dim objModuleControl As ModuleControlInfo
                Dim intCounter As Integer

                Dim arrModuleControls As ArrayList = ModuleControlController.GetModuleControlsByKey(Key, slaveModule.ModuleDefID)

                For intCounter = 0 To arrModuleControls.Count - 1

                    objModuleControl = CType(arrModuleControls(intCounter), ModuleControlInfo)

                    ' initialize control values
                    slaveModule.ModuleControlId = objModuleControl.ModuleControlID
                    slaveModule.ControlSrc = objModuleControl.ControlSrc
                    slaveModule.ControlType = objModuleControl.ControlType
                    slaveModule.IconFile = objModuleControl.IconFile
                    slaveModule.HelpUrl = objModuleControl.HelpURL
                    slaveModule.SupportsPartialRendering = objModuleControl.SupportsPartialRendering

					' try to localize control title
					slaveModule.ModuleTitle = Localization.LocalizeControlTitle(objModuleControl.ControlTitle, slaveModule.ControlSrc, Key)

					' verify that the current user has access to this control
					If PortalSecurity.HasNecessaryPermission(slaveModule.ControlType, PortalSettings, slaveModule) Then
						'try to inject the module into the skin
						Try
							InjectModule(parent, slaveModule, PortalSettings)
						Catch ex As Exception
							bSuccess = False
						End Try
					Else
						Response.Redirect(AccessDeniedURL(MODULEACCESS_ERROR), True)
					End If

				Next

			End If

			'if querystring dnnprintmode=true, controlpanel will not be shown
			If Request.QueryString("dnnprintmode") <> "true" Then
				' ControlPanel processing
				If (PortalSettings.ControlPanelSecurity = PortalSettings.ControlPanelPermission.ModuleEditor And m_blnHasModuleEditPermissions = True) OrElse (PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName.ToString) = True OrElse PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = True) Then
					Dim objControlPanel As UserControl = Nothing
					If Convert.ToString(PortalSettings.HostSettings("ControlPanel")) <> "" Then
						' load custom control panel
						objControlPanel = CType(LoadControl("~/" & Convert.ToString(PortalSettings.HostSettings("ControlPanel"))), UserControl)
					End If
					If objControlPanel Is Nothing Then
						' load default control panel
						objControlPanel = CType(LoadControl("~/" & glbDefaultControlPanel), UserControl)
					End If
					' inject ControlPanel control into skin
					ctlPane = Me.FindControl("ControlPanel")
					If ctlPane Is Nothing Then
						Dim objForm As HtmlForm = CType(Me.Parent.FindControl("Form"), HtmlForm)
						objForm.Controls.AddAt(0, objControlPanel)
					Else
						ctlPane.Controls.Add(objControlPanel)
					End If
				End If
			End If

			If Not blnLayoutMode Then
				CollapseUnusedPanes()
			End If

			If Not Request.QueryString("error") Is Nothing Then
				Skin.AddPageMessage(Me, CRITICAL_ERROR, Server.HtmlEncode(Request.QueryString("error")), UI.Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
			End If

			If Not (PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = True Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = True) Then
				' only display the warning to non-administrators (adminsitrators will see the errors)
				If Not bSuccess Then
					Skin.AddPageMessage(Me, MODULELOAD_WARNING, String.Format(MODULELOAD_WARNINGTEXT, PortalSettings.Email), UI.Skins.Controls.ModuleMessage.ModuleMessageType.YellowWarning)
				End If
			End If

        End Sub

        Public Sub ModuleAction_Click(ByVal sender As Object, ByVal e As ActionEventArgs)
            'Search through the listeners
            Dim Listener As ModuleActionEventListener
            For Each Listener In ActionEventListeners

                'If the associated module has registered a listener
                If e.ModuleConfiguration.ModuleID = Listener.ModuleID Then

                    'Invoke the listener to handle the ModuleAction_Click event
                    Listener.ActionEvent.Invoke(sender, e)
                End If
            Next
        End Sub

#End Region

#Region " Web Form Designer Generated Code "

        'This call is required by the Web Form Designer.
        <System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()

        End Sub

#End Region

    End Class

End Namespace
