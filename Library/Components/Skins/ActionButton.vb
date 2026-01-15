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

Imports DotNetNuke.Entities.Modules.Actions

Namespace DotNetNuke.UI.Containers

    Public Class ActionButton
        Inherits ActionBase

#Region "Private Members"

        Private _buttonSeparator As String = "&nbsp;&nbsp;"
        Private _commandName As String = ""
        Private _cssClass As String = "CommandButton"
        Private _displayLink As Boolean = True
        Private _displayIcon As Boolean = False
        Private _iconFile As String

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the Command Name
        ''' </summary>
        ''' <remarks>Maps to ModuleActionType in DotNetNuke.Entities.Modules.Actions</remarks>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	6/29/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property CommandName() As String
            Get
                Return _commandName
            End Get
            Set(ByVal Value As String)
                _commandName = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the CSS Class
        ''' </summary>
        ''' <remarks>Defaults to 'CommandButton'</remarks>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	6/29/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property CssClass() As String
            Get
                Return _cssClass
            End Get
            Set(ByVal Value As String)
                _cssClass = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets whether the link is displayed
        ''' </summary>
        ''' <remarks>Defaults to True</remarks>
        ''' <value>A Boolean</value>
        ''' <history>
        ''' 	[cnurse]	6/29/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property DisplayLink() As Boolean
            Get
                Return _displayLink
            End Get
            Set(ByVal Value As Boolean)
                _displayLink = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets whether the icon is displayed
        ''' </summary>
        ''' <remarks>Defaults to False</remarks>
        ''' <value>A Boolean</value>
        ''' <history>
        ''' 	[cnurse]	6/29/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property DisplayIcon() As Boolean
            Get
                Return _displayIcon
            End Get
            Set(ByVal Value As Boolean)
                _displayIcon = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the Icon used
        ''' </summary>
        ''' <remarks>Defaults to the icon defined in Action</remarks>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	6/29/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property IconFile() As String
            Get
                Return _iconFile
            End Get
            Set(ByVal Value As String)
                _iconFile = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the Separator between Buttons
        ''' </summary>
        ''' <remarks>Defaults to 2 non-breaking spaces</remarks>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	6/29/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property ButtonSeparator() As String
            Get
                Return _buttonSeparator
            End Get
            Set(ByVal Value As String)
                _buttonSeparator = Value
            End Set
        End Property

#End Region

#Region "Private Methods"

        Private Sub GetClientScriptURL(ByVal Action As ModuleAction, ByVal control As WebControl)

            If Len(Action.ClientScript) > 0 Then
                Dim Script As String = Action.ClientScript

                Dim JSPos As Integer = Script.ToLower.IndexOf("javascript:")
                If JSPos > -1 Then
                    Script = Script.Substring(JSPos + 11)
                End If

                Dim FormatScript As String = "javascript: return {0};"

                control.Attributes.Add("onClick", String.Format(FormatScript, Script))
            End If

        End Sub

        Private Sub LinkAction_Click(ByVal sender As Object, ByVal e As System.EventArgs)
            Try
                ProcessAction(DirectCast(sender, LinkButton).ID.Substring(3))
            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try

        End Sub

#End Region

#Region "Event Handlers"

        Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try
                For Each action As ModuleAction In Me.MenuActions
                    If action.CommandName = CommandName Then
                        If action.Visible = True And PortalSecurity.HasNecessaryPermission(action.Secure, PortalSettings, PortalModule.ModuleConfiguration) = True Then
                            If (PortalSettings.UserMode = PortalSettings.Mode.Edit Or PortalModule.PortalSettings.ActiveTab.IsAdminTab = True) Or (action.Secure = SecurityAccessLevel.Anonymous Or action.Secure = SecurityAccessLevel.View) Then
                                If action.CommandName = ModuleActionType.PrintModule And PortalModule.ModuleConfiguration.DisplayPrint = False Then
                                    DisplayIcon = False
                                    DisplayLink = False
                                End If
                                If action.CommandName = ModuleActionType.SyndicateModule And PortalModule.ModuleConfiguration.DisplaySyndicate = False Then
                                    DisplayIcon = False
                                    DisplayLink = False
                                End If

                                If DisplayIcon = True And (action.Icon <> "" Or IconFile <> "") Then
                                    Dim ModuleActionIcon As New System.Web.UI.WebControls.Image            'New ImageButton
                                    Dim ModuleActionLink As New LinkButton
                                    If IconFile <> "" Then
                                        ModuleActionIcon.ImageUrl = PortalModule.ModuleConfiguration.ContainerPath.Substring(0, PortalModule.ModuleConfiguration.ContainerPath.LastIndexOf("/") + 1) & IconFile
                                    Else
                                        If action.Icon.IndexOf("/") > 0 Then
                                            ModuleActionIcon.ImageUrl = action.Icon
                                        Else
                                            ModuleActionIcon.ImageUrl = "~/images/" & action.Icon
                                        End If
                                    End If
                                    ModuleActionIcon.ToolTip = action.Title
                                    ModuleActionIcon.AlternateText = action.Title
                                    ModuleActionLink.ID = "ico" & action.ID.ToString
                                    ModuleActionLink.CausesValidation = False
                                    ModuleActionLink.EnableViewState = False
                                    GetClientScriptURL(action, ModuleActionLink)

                                    AddHandler ModuleActionLink.Click, AddressOf LinkAction_Click          'IconAction_Click

                                    ModuleActionLink.Controls.Add(ModuleActionIcon)
                                    Me.Controls.Add(ModuleActionLink)
                                End If

                                If DisplayLink Then
                                    Dim ModuleActionLink As New LinkButton
                                    ModuleActionLink.Text = action.Title
                                    ModuleActionLink.ToolTip = action.Title
                                    ModuleActionLink.CssClass = CssClass
                                    ModuleActionLink.ID = "lnk" & action.ID.ToString
                                    ModuleActionLink.CausesValidation = False
                                    ModuleActionLink.EnableViewState = False
                                    GetClientScriptURL(action, ModuleActionLink)

                                    AddHandler ModuleActionLink.Click, AddressOf LinkAction_Click

                                    Me.Controls.Add(ModuleActionLink)
                                End If

                                If DisplayLink Or DisplayIcon Then
                                    Me.Controls.Add(New LiteralControl(ButtonSeparator))
                                End If
                            End If
                        End If
                    End If
                Next

                ' set visibility
                If Me.Controls.Count > 0 Then
                    Me.Visible = True
                Else
                    Me.Visible = False
                End If

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try

        End Sub

#End Region

    End Class

End Namespace