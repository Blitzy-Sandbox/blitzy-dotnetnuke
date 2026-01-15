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

Imports System
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Globalization
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Web.UI
Imports System.Web.UI.HtmlControls
Imports System.Web.UI.WebControls

Imports DotNetNuke.Common
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals

Namespace DotNetNuke.Framework

    ''' -----------------------------------------------------------------------------
    ''' Namespace:  DotNetNuke.Framework
    ''' Project:    DotNetNuke
    ''' Class:      PageBase
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' PageBase provides a custom DotNetNuke base class for pages
    ''' </summary>
    ''' <history>
    '''		[cnurse]	11/30/2006	documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public MustInherit Class PageBase
        Inherits System.Web.UI.Page

#Region "Private Members"

        Private _localizedControls As ArrayList
        Private _localResourceFile As String

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Creates the Page
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	    11/30/2006	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New()
            _localizedControls = New ArrayList
        End Sub    'New

#End Region

#Region "Protected Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' PageStatePersister returns an instance of the class that will be used to persist the Page State
        ''' </summary>
        ''' <returns>A System.Web.UI.PageStatePersister</returns>
        ''' <history>
        ''' 	[cnurse]	    11/30/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property PageStatePersister() As System.Web.UI.PageStatePersister
            Get
                'Set ViewState Persister to default (as defined in Base Class)
                Dim _persister As PageStatePersister = MyBase.PageStatePersister
                If Not DotNetNuke.Common.Globals.HostSettings("PageStatePersister") Is Nothing Then
                    Select Case DirectCast(DotNetNuke.Common.Globals.HostSettings("PageStatePersister"), String)
                        Case "M"
                            _persister = New CachePageStatePersister(Me)
                        Case "D"
                            _persister = New DiskPageStatePersister(Me)
                        Case "S"
                            _persister = New SessionPageStatePersister(Me)
                    End Select
                End If
                Return _persister
            End Get
        End Property
#End Region

#Region "Public Properties"


        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property PortalSettings() As PortalSettings

            Get
                PortalSettings = PortalController.GetCurrentPortalSettings
            End Get

        End Property

        Public Property PageCulture() As CultureInfo
            Get
                Dim enabledLocales As LocaleCollection = DotNetNuke.Services.Localization.Localization.GetEnabledLocales()
                Dim ci As CultureInfo = Nothing
                'used as temporary variable to get info about the preferred locale
                Dim preferredLocale As String = ""
                'used as temporary variable where the language part of the preferred locale will be saved
                Dim preferredLanguage As String = ""

                'first try if a specific language is requested by cookie, querystring, or form
                If Not (HttpContext.Current Is Nothing) Then
                    Try
                        preferredLocale = HttpContext.Current.Request("language")
                        If preferredLocale <> "" Then
                            If Services.Localization.Localization.LocaleIsEnabled(preferredLocale) Then
                                ci = New CultureInfo(preferredLocale)
                            Else
                                preferredLanguage = preferredLocale.Split("-"c)(0)
                            End If
                        End If
                    Catch
                    End Try
                End If

                If ci Is Nothing Then
                    ' next try to get the preferred language of the logged on user
                    Dim objUserInfo As UserInfo = UserController.GetCurrentUserInfo
                    If objUserInfo.UserID <> -1 Then
                        If objUserInfo.Profile.PreferredLocale <> "" Then
                            If Services.Localization.Localization.LocaleIsEnabled(preferredLocale) Then
                                ci = New CultureInfo(objUserInfo.Profile.PreferredLocale)
                            Else
                                If preferredLanguage = "" Then
                                    preferredLanguage = objUserInfo.Profile.PreferredLocale.Split("-"c)(0)
                                End If
                            End If
                        End If
                    End If
                End If

                If ci Is Nothing AndAlso Localization.UseBrowserLanguage() Then
                    ' use Request.UserLanguages to get the preferred language
                    If Not (HttpContext.Current Is Nothing) Then
                        If Not (HttpContext.Current.Request.UserLanguages Is Nothing) Then
                            Try
                                For Each userLang As String In HttpContext.Current.Request.UserLanguages
                                    'split userlanguage by ";"... all but the first language will contain a preferrence index eg. ;q=.5
                                    Dim userlanguage As String = userLang.Split(";"c)(0)
                                    If Services.Localization.Localization.LocaleIsEnabled(userlanguage) Then
                                        ci = New CultureInfo(userlanguage)
                                    ElseIf userLang.Split(";"c)(0).IndexOf("-") <> -1 Then
                                        'if userLang is neutral we don't need to do this part since
                                        'it has already been done in LocaleIsEnabled( )
                                        Dim templang As String = userLang.Split(";"c)(0)
                                        For Each _localeCode As String In enabledLocales.AllKeys
                                            If _localeCode.Split("-"c)(0) = templang.Split("-"c)(0) Then
                                                'the preferredLanguage was found in the enabled locales collection, so we are going to use this one
                                                'eg, requested locale is en-GB, requested language is en, enabled locale is en-US, so en is a match for en-US
                                                ci = New CultureInfo(_localeCode)
                                                Exit For
                                            End If
                                        Next
                                    End If
                                    If Not ci Is Nothing Then
                                        Exit For
                                    End If
                                Next
                            Catch
                            End Try
                        End If
                    End If
                End If

                If ci Is Nothing And preferredLanguage <> "" Then
                    'we still don't have a good culture, so we are going to try to get a culture with the preferredlanguage instead
                    For Each _localeCode As String In enabledLocales.AllKeys
                        If _localeCode.Split("-"c)(0) = preferredLanguage Then
                            'the preferredLanguage was found in the enabled locales collection, so we are going to use this one
                            'eg, requested locale is en-GB, requested language is en, enabled locale is en-US, so en is a match for en-US
                            ci = New CultureInfo(_localeCode)
                            Exit For
                        End If
                    Next
                End If

                'we still have no culture info set, so we are going to use the fallback method
                If ci Is Nothing Then
                    If PortalSettings.DefaultLanguage = "" Then
                        ' this is a last resort, as the portal default language should always be set
                        ' however if its not set, return the first enabled locale
                        ' if there are no enabled locales, return the systemlocale
                        If enabledLocales.Count > 0 Then
                            ci = New CultureInfo(CType(enabledLocales(0).Key, String))
                        Else
                            ci = New CultureInfo(Services.Localization.Localization.SystemLocale)
                        End If
                    Else
                        ' as the portal default language can never be disabled, we know this language is available and enabled
                        ci = New CultureInfo(PortalSettings.DefaultLanguage)
                    End If
                End If

                If ci Is Nothing Then
                    'just a safeguard, to make sure we return something
                    ci = New CultureInfo(Services.Localization.Localization.SystemLocale)
                End If

                'finally set the cookie
                DotNetNuke.Services.Localization.Localization.SetLanguage(ci.Name)
                Return ci

            End Get
            Set(ByVal Value As CultureInfo)
                Thread.CurrentThread.CurrentUICulture = PageCulture
                Thread.CurrentThread.CurrentCulture = PageCulture
            End Set
        End Property


        Public Property LocalResourceFile() As String
            Get
                Dim fileRoot As String
                Dim page As String() = Request.ServerVariables("SCRIPT_NAME").Split("/"c)

                If _localResourceFile = "" Then
                    fileRoot = Me.TemplateSourceDirectory & "/" & Services.Localization.Localization.LocalResourceDirectory & "/" & page(page.GetUpperBound(0)) & ".resx"
                Else
                    fileRoot = _localResourceFile
                End If
                Return fileRoot
            End Get
            Set(ByVal Value As String)
                _localResourceFile = Value
            End Set
        End Property

#End Region

#Region "Public Methods"

        Protected Overrides Sub OnInit(ByVal e As System.EventArgs)
            ' Set the current culture
            Thread.CurrentThread.CurrentUICulture = PageCulture
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture
            ' Localize portalsettings
            Services.Localization.Localization.LocalizePortalSettings()
            MyBase.OnInit(e)
        End Sub

        Public Function HasTabPermission(ByVal PermissionKey As String) As Boolean
            Return Security.Permissions.TabPermissionController.HasTabPermission(PortalSettings.ActiveTab.TabPermissions, PermissionKey)
        End Function


        ''' <summary>
        ''' <para>ProcessControl peforms the high level localization for a single control and optionally it's children.</para>
        ''' </summary>
        ''' <param name="c">Control to find the AttributeCollection on</param>
        ''' <param name="affectedControls">ArrayList that hold the controls that have been localized. This is later used for the removal of the key attribute.</param>				
        ''' <param name="includeChildren">If true, causes this method to process children of this controls.</param>
        Friend Sub ProcessControl(ByVal c As Control, ByVal affectedControls As ArrayList, ByVal includeChildren As Boolean, ByVal ResourceFileRoot As String)
            ' Perform the substitution if a key was found			
            Dim key As String = GetControlAttribute(c, affectedControls)
            If Not key Is Nothing Then
                'Translation starts here ....
                Dim value As String
                value = Services.Localization.Localization.GetString(key, ResourceFileRoot)

                If TypeOf c Is Label Then
                    Dim ctrl As Label
                    ctrl = DirectCast(c, Label)
                    If value <> "" Then
                        ctrl.Text = value
                    End If
                End If
                If TypeOf c Is LinkButton Then
                    Dim ctrl As LinkButton
                    ctrl = DirectCast(c, LinkButton)
                    If value <> "" Then

                        Dim imgMatches As MatchCollection = Regex.Matches(value, "<(a|link|img|script|input|form).[^>]*(href|src|action)=(\""|'|)(.[^\""']*)(\""|'|)[^>]*>", RegexOptions.IgnoreCase)

                        For Each _match As Match In imgMatches

                            If (_match.Groups(_match.Groups.Count - 2).Value.IndexOf("~") <> -1) Then

                                Dim resolvedUrl As String = Page.ResolveUrl(_match.Groups(_match.Groups.Count - 2).Value)

                                value = value.Replace(_match.Groups(_match.Groups.Count - 2).Value, resolvedUrl)

                            End If

                        Next

                        ctrl.Text = value
                    End If
                End If
                If TypeOf c Is HyperLink Then
                    Dim ctrl As HyperLink
                    ctrl = DirectCast(c, HyperLink)
                    If value <> "" Then
                        ctrl.Text = value
                    End If
                End If
                If TypeOf c Is ImageButton Then
                    Dim ctrl As ImageButton
                    ctrl = DirectCast(c, ImageButton)
                    If value <> "" Then
                        ctrl.AlternateText = value
                    End If
                End If
                If TypeOf c Is Button Then
                    Dim ctrl As Button
                    ctrl = DirectCast(c, Button)
                    If value <> "" Then
                        ctrl.Text = value
                    End If
                End If
                If TypeOf c Is HtmlControls.HtmlImage Then
                    Dim ctrl As HtmlControls.HtmlImage
                    ctrl = DirectCast(c, HtmlControls.HtmlImage)
                    If value <> "" Then
                        ctrl.Alt = value
                    End If
                End If
                If TypeOf c Is CheckBox Then
                    Dim ctrl As CheckBox
                    ctrl = DirectCast(c, CheckBox)
                    If value <> "" Then
                        ctrl.Text = value
                    End If
                End If
                If TypeOf c Is BaseValidator Then
                    Dim ctrl As BaseValidator
                    ctrl = DirectCast(c, BaseValidator)
                    If value <> "" Then
                        ctrl.ErrorMessage = value
                    End If
                End If
                If TypeOf c Is System.Web.UI.WebControls.Image Then
                    Dim ctrl As System.Web.UI.WebControls.Image
                    ctrl = DirectCast(c, System.Web.UI.WebControls.Image)
                    If value <> "" Then
                        ctrl.AlternateText = value
                        ctrl.ToolTip = value
                    End If
                End If
            End If

            ' Translate radiobuttonlist items here 
            If TypeOf c Is RadioButtonList Then
                Dim ctrl As RadioButtonList
                ctrl = DirectCast(c, RadioButtonList)
                Dim i As Integer
                For i = 0 To ctrl.Items.Count - 1
                    Dim ac As System.Web.UI.AttributeCollection = Nothing
                    ac = ctrl.Items(i).Attributes
                    key = ac(Services.Localization.Localization.KeyName)
                    If Not key Is Nothing Then
                        Dim value As String = Services.Localization.Localization.GetString(key, ResourceFileRoot)
                        If value <> "" Then
                            ctrl.Items(i).Text = value
                        End If
                    End If
                Next
            End If

            ' Translate dropdownlist items here 
            If TypeOf c Is DropDownList Then
                Dim ctrl As DropDownList
                ctrl = DirectCast(c, DropDownList)
                Dim i As Integer
                For i = 0 To ctrl.Items.Count - 1
                    Dim ac As System.Web.UI.AttributeCollection = Nothing
                    ac = ctrl.Items(i).Attributes
                    key = ac(Services.Localization.Localization.KeyName)
                    If Not key Is Nothing Then
                        Dim value As String = Services.Localization.Localization.GetString(key, ResourceFileRoot)
                        If value <> "" Then
                            ctrl.Items(i).Text = value
                        End If
                    End If
                Next
            End If

            '' UrlRewriting Issue - ResolveClientUrl gets called instead of ResolveUrl
            '' Manual Override to ResolveUrl
            If TypeOf c Is System.Web.UI.WebControls.Image Then
                Dim ctrl As System.Web.UI.WebControls.Image
                ctrl = DirectCast(c, System.Web.UI.WebControls.Image)
                If (ctrl.ImageUrl.IndexOf("~") <> -1) Then
                    ctrl.ImageUrl = Page.ResolveUrl(ctrl.ImageUrl)
                End If
            End If

            ' UrlRewriting Issue - ResolveClientUrl gets called instead of ResolveUrl
            ' Manual Override to ResolveUrl
            If TypeOf c Is HtmlControls.HtmlImage Then
                Dim ctrl As HtmlControls.HtmlImage
                ctrl = DirectCast(c, HtmlControls.HtmlImage)
                If (ctrl.Src.IndexOf("~") <> -1) Then
                    ctrl.Src = Page.ResolveUrl(ctrl.Src)
                End If
            End If

            ' UrlRewriting Issue - ResolveClientUrl gets called instead of ResolveUrl
            ' Manual Override to ResolveUrl
            If TypeOf c Is System.Web.UI.WebControls.HyperLink Then
                Dim ctrl As System.Web.UI.WebControls.HyperLink
                ctrl = DirectCast(c, System.Web.UI.WebControls.HyperLink)
                If (ctrl.NavigateUrl.IndexOf("~") <> -1) Then
                    ctrl.NavigateUrl = Page.ResolveUrl(ctrl.NavigateUrl)
                End If
                If (ctrl.ImageUrl.IndexOf("~") <> -1) Then
                    ctrl.ImageUrl = Page.ResolveUrl(ctrl.ImageUrl)
                End If
            End If

            ' Process child controls
            If includeChildren = True And c.HasControls() Then
                If TypeOf c Is PortalModuleBase Then
                    'Get Resource File Root from Controls LocalResourceFile Property
                    Dim ctrl As Entities.Modules.PortalModuleBase
                    ctrl = DirectCast(c, PortalModuleBase)
                    IterateControls(c.Controls, affectedControls, ctrl.LocalResourceFile)
                Else
                    Dim pi As PropertyInfo = c.GetType.GetProperty("LocalResourceFile")
                    If Not pi Is Nothing AndAlso Not pi.GetValue(c, Nothing) Is Nothing Then
                        'If controls has a LocalResourceFile property use this
                        IterateControls(c.Controls, affectedControls, pi.GetValue(c, Nothing).ToString())
                    Else
                        'Pass Resource File Root through
                        IterateControls(c.Controls, affectedControls, ResourceFileRoot)
                    End If
                End If
            End If

        End Sub    'ProcessControl

        ''' <summary>
        ''' <para>GetControlAttribute looks a the type of control and does it's best to find an AttributeCollection.</para>
        ''' </summary>
        ''' <param name="c">Control to find the AttributeCollection on</param>
        ''' <param name="affectedControls">ArrayList that hold the controls that have been localized. This is later used for the removal of the key attribute.</param>				
        ''' <returns>A string containing the key for the specified control or null if a key attribute wasn't found</returns>
        Friend Shared Function GetControlAttribute(ByVal c As Control, ByVal affectedControls As ArrayList) As String
            Dim ac As System.Web.UI.AttributeCollection = Nothing
            Dim key As String = Nothing

            If TypeOf c Is LiteralControl Then    ' LiteralControls don't have an attribute collection
                key = Nothing
                ac = Nothing
            Else
                If TypeOf c Is WebControl Then
                    Dim w As WebControl = DirectCast(c, WebControl)
                    ac = w.Attributes
                    key = ac(Services.Localization.Localization.KeyName)
                Else
                    If TypeOf c Is HtmlControl Then
                        Dim h As HtmlControl = DirectCast(c, HtmlControl)
                        ac = h.Attributes
                        key = ac(Services.Localization.Localization.KeyName)
                    Else
                        If TypeOf c Is UserControl Then
                            Dim u As UserControl = DirectCast(c, UserControl)
                            ac = u.Attributes
                            key = ac(Services.Localization.Localization.KeyName)
                            ' Use reflection to check for attribute key. This is a last ditch option
                        Else
                            Dim controlType As Type = c.GetType()
                            Dim attributeProperty As PropertyInfo = controlType.GetProperty("Attributes", GetType(System.Web.UI.AttributeCollection))
                            If Not attributeProperty Is Nothing Then
                                ac = DirectCast(attributeProperty.GetValue(c, Nothing), System.Web.UI.AttributeCollection)
                                key = ac(Services.Localization.Localization.KeyName)
                            End If
                        End If
                    End If
                End If    ' If the key was found add this AttributeCollection to the list that should have the key removed during Render
            End If
            If Not key Is Nothing And Not affectedControls Is Nothing Then
                affectedControls.Add(ac)
            End If
            Return key
        End Function    'GetControlAttribute

        ''' <summary>
        ''' <para>RemoveKeyAttribute remove the key attribute from the control. If this isn't done, then the HTML output will have 
        ''' a bad attribute on it which could cause some older browsers problems.</para>
        ''' </summary>
        ''' <param name="affectedControls">ArrayList that hold the controls that have been localized. This is later used for the removal of the key attribute.</param>		
        Public Shared Sub RemoveKeyAttribute(ByVal affectedControls As ArrayList)
            If affectedControls Is Nothing Then
                Return
            End If
            Dim i As Integer
            For i = 0 To affectedControls.Count - 1
                Dim ac As System.Web.UI.AttributeCollection = DirectCast(affectedControls(i), System.Web.UI.AttributeCollection)
                ac.Remove(Services.Localization.Localization.KeyName)
            Next i
        End Sub    'RemoveKeyAttribute

#End Region

#Region "Private Helpers"

        Private Sub Page_Error(ByVal Source As Object, ByVal e As System.EventArgs) Handles MyBase.Error
            Dim exc As Exception = Server.GetLastError
            Dim strURL As String = ApplicationURL()

            Dim objBasePortalException As BasePortalException = New BasePortalException(exc.ToString, exc)
            If objBasePortalException.Message.Contains("System.Web.Extensions") Then
                ' suppress AJAX error in Medium Trust
                Response.Redirect(strURL)
            Else
                If Not Request.QueryString("error") Is Nothing Then
                    strURL += IIf(strURL.IndexOf("?") = -1, "?", "&").ToString & "error=terminate"
                Else
                    strURL += IIf(strURL.IndexOf("?") = -1, "?", "&").ToString & "error=" & Server.UrlEncode(exc.Message)
                    If Not IsAdminControl() Then
                        strURL += "&content=0"
                    End If
                End If
                ProcessPageLoadException(exc, strURL)
            End If
        End Sub

        ''' <summary>
        ''' <seealso cref="System.Web.UI.Control.Render" />
        ''' </summary>
        ''' <param name="evt">An EventArgs that controls the event data.</param>
        Protected Overrides Sub OnPreRender(ByVal evt As EventArgs)
            MyBase.OnPreRender(evt)
        End Sub    'OnPreRender

        Protected Overrides Sub Render(ByVal writer As System.Web.UI.HtmlTextWriter)
            'Localize controls
            IterateControls(Controls, _localizedControls, LocalResourceFile)
            RemoveKeyAttribute(_localizedControls)

            ' remove ASP.NET ScriptManager if not used on page
            AJAX.RemoveScriptManager(Me)

            MyBase.Render(writer)
        End Sub

        ''' <summary>
        ''' <para>IterateControls performs the high level localization for each control on the page.</para>
        ''' </summary>
        Private Overloads Sub IterateControls(ByVal affectedControls As ArrayList)
            IterateControls(Controls, affectedControls, Nothing)
        End Sub    'IterateControls

        Private Overloads Sub IterateControls(ByVal controls As ControlCollection, ByVal affectedControls As ArrayList, ByVal ResourceFileRoot As String)
            For Each c As Control In controls
                ProcessControl(c, affectedControls, True, ResourceFileRoot)
            Next
        End Sub    'IterateControls

#End Region

#Region "Obsolete Methods"

        <Obsolete("This function has been replaced by DotNetNuke.Services.Localization.Localization.SetLanguage")> _
        Public Sub SetLanguage(ByVal value As String)
            DotNetNuke.Services.Localization.Localization.SetLanguage(value)
        End Sub

#End Region

    End Class 'PageBase

End Namespace
