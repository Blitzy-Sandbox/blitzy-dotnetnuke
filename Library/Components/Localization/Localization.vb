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
Imports System.IO
Imports System.Web.Caching
Imports System.Threading
Imports System.Resources
Imports System.Collections.Specialized
Imports System.Diagnostics
Imports System.Reflection
Imports System.Xml
Imports System.Xml.XPath

Imports DotNetNuke.Entities.Profile
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Services.Tokens


Namespace DotNetNuke.Services.Localization

    ''' <summary>
    ''' <para>CultureDropDownTypes allows the user to specify which culture name is displayed in the drop down list that is filled 
    ''' by using one of the helper methods.</para>
    ''' </summary>
    <Serializable()> _
    Public Enum CultureDropDownTypes
        'Displays the culture name in the format "&lt;languagefull&gt; (&lt;country/regionfull&gt;) in the .NET Framework language
        DisplayName
        'Displays the culture name in the format "&lt;languagefull&gt; (&lt;country/regionfull&gt;) in English
        EnglishName
        'Displays the culture identifier
        Lcid
        'Displays the culture name in the format "&lt;languagecode2&gt; (&lt;country/regioncode2&gt;)
        Name
        'Displays the culture name in the format "&lt;languagefull&gt; (&lt;country/regionfull&gt;) in the language that the culture is set to display
        NativeName
        'Displays the IS0 639-1 two letter code
        TwoLetterIsoCode
        'Displays the ISO 629-2 three letter code "&lt;languagefull&gt; (&lt;country/regionfull&gt;)
        ThreeLetterIsoCode
    End Enum

    Public Class Localization

        Private Shared strShowMissingKeys As String = ""
        Private Shared strUseBrowserLanguageDefault As String = ""
        Private Shared strUseLanguageInUrlDefault As String = ""

#Region "Constants"
        Friend Const keyConst As String = "resourcekey"
        Public Const SystemLocale As String = "en-US"
        Public Const SharedResourceFile As String = ApplicationResourceDirectory + "/SharedResources.resx"
        Public Const LocalResourceDirectory As String = "App_LocalResources"
        Public Const LocalSharedResourceFile As String = "SharedResources.resx"
        Public Const ApplicationResourceDirectory As String = "~/App_GlobalResources"
        Public Const GlobalResourceFile As String = ApplicationResourceDirectory + "/GlobalResources.resx"
        Public Const SupportedLocalesFile As String = ApplicationResourceDirectory + "/Locales.xml"
        Public Const TimezonesFile As String = ApplicationResourceDirectory + "/TimeZones.xml"
        Public Const SystemTimeZoneOffset As Integer = -480
#End Region

#Region "Private Enums"
        Private Enum CustomizedLocale
            None = 0
            Portal = 1
            Host = 2
        End Enum
#End Region

#Region "Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CurrentCulture returns the current Culture being used
        ''' is 'key'.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property CurrentCulture() As String
            Get
                Return System.Threading.Thread.CurrentThread.CurrentCulture.ToString    ' _CurrentCulture
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The KeyName property returns and caches the name of the key attribute used to lookup resources.
        ''' This can be configured by setting ResourceManagerKey property in the web.config file. The default value for this property
        ''' is 'key'.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Property KeyName() As String
            Get
                Return _defaultKeyName
            End Get
            Set(ByVal Value As String)
                _defaultKeyName = Value
                If _defaultKeyName Is Nothing Or _defaultKeyName = String.Empty Then
                    _defaultKeyName = keyConst
                End If
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ShowMissingKeys property returns the web.config setting that determines 
        ''' whether to render a visual indicator that a key is missing
        ''' is 'key'.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/20/2006	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared ReadOnly Property ShowMissingKeys() As Boolean
            Get
                If String.IsNullOrEmpty(strShowMissingKeys) Then
                    If Config.GetSetting("ShowMissingKeys") Is Nothing Then
                        strShowMissingKeys = "false"
                    Else
                        strShowMissingKeys = Config.GetSetting("ShowMissingKeys").ToLower()
                    End If
                End If
                Return Boolean.Parse(strShowMissingKeys)
            End Get
        End Property

#End Region

#Region "Private Members"
        Private Shared _defaultKeyName As String = keyConst
        Private Shared _timeZoneListItems() As ListItem
#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetResource is used by GetString to load the resources Hashtable
        ''' </summary>
        ''' <remarks>
        '''     The priority of cultures is User > Portal Default > FallBack
        '''
        '''     Therefore the logic used is the following:
        '''     1. Load the fallback language resources first (these are known
        '''         to exist, thus making sure that all keys are loaded)
        '''     2. Next load the Portal's default language if different (any keys that
        '''         are present overwrite the fallback keys).
        '''     3. Last load the user's language if different from either of the above
        '''         (again any keys that are present overwrite the fallback/default keys).
        '''
        ''' </remarks>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <param name="objPortalSettings">The current portals Portal Settings</param>
        ''' <returns>The rsources Hashtable </returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetResource(ByVal ResourceFileRoot As String, ByVal objPortalSettings As PortalSettings) As Hashtable
            Return GetResource(ResourceFileRoot, objPortalSettings, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetResource is used by GetString to load the resources Hashtable
        ''' </summary>
        ''' <remarks>
        '''     The priority of cultures is User > Portal Default > FallBack
        '''
        '''     Therefore the logic used is the following:
        '''     1. Load the fallback language resources first (these are known
        '''         to exist, thus making sure that all keys are loaded)
        '''     2. Next load the Portal's default language if different (any keys that
        '''         are present overwrite the fallback keys).
        '''     3. Last load the user's language if different from either of the above
        '''         (again any keys that are present overwrite the fallback/default keys).
        '''
        ''' </remarks>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <param name="objPortalSettings">The current portals Portal Settings</param>
        ''' <param name="strLanguage">A specific language used to get the resource</param>
        ''' <returns>The resources Hashtable </returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetResource(ByVal ResourceFileRoot As String, ByVal objPortalSettings As PortalSettings, ByVal strLanguage As String) As Hashtable

            Dim resources As Hashtable
            Dim defaultLanguage As String = Null.NullString
            Dim userLanguage As String
            Dim fallbackLanguage As String = SystemLocale.ToLower
            Dim portalId As Integer = Null.NullInteger
            Dim userFile As String
            Dim defaultFile As String
            Dim fallbackFile As String

            If Not objPortalSettings Is Nothing Then
                defaultLanguage = objPortalSettings.DefaultLanguage.ToLower
                portalId = objPortalSettings.PortalId
            End If

            If strLanguage Is Nothing Then
                userLanguage = Thread.CurrentThread.CurrentCulture.ToString.ToLower
            Else
                userLanguage = strLanguage
            End If

            ' Ensure the user has a language set
            If userLanguage = "" Then
                userLanguage = defaultLanguage
            End If

            Dim userLocale As Locale = GetSupportedLocales(userLanguage)
            If (Not userLocale Is Nothing) AndAlso (userLocale.Fallback.ToLower <> "") Then
                fallbackLanguage = userLocale.Fallback.ToLower
            End If

            'Get the filename for the userlanguage version of the resource file
            userFile = GetResourceFileName(ResourceFileRoot, userLanguage)

            'Set the cachekey as the userFile + the PortalId
            Dim cacheKey As String = userFile.Replace("~/", "/").ToLower + portalId.ToString
            If Not String.IsNullOrEmpty(ApplicationPath) Then
                cacheKey = cacheKey.Replace(ApplicationPath, "")
            End If

            'Attempt to get the resources from the cache
            resources = CType(DataCache.GetCache(cacheKey), Hashtable)

            If resources Is Nothing Then
                ' resources not in Cache so load from Files

                'Create resources Hashtable
                resources = New Hashtable

                'First Load the Fallback Language ensuring that the keys are loaded
                fallbackFile = GetResourceFileName(ResourceFileRoot, fallbackLanguage)
                resources = LoadResource(resources, fallbackLanguage, cacheKey, fallbackFile, CustomizedLocale.None, objPortalSettings)
                ' Add any host level customizations
                resources = LoadResource(resources, fallbackLanguage, cacheKey, fallbackFile, CustomizedLocale.Host, objPortalSettings)
                ' Add any portal level customizations
                If Not objPortalSettings Is Nothing Then
                    resources = LoadResource(resources, fallbackLanguage, cacheKey, fallbackFile, CustomizedLocale.Portal, objPortalSettings)
                End If

                'if the defaultLanguage is different, load it
                If defaultLanguage <> "" AndAlso defaultLanguage <> fallbackLanguage AndAlso userLanguage <> fallbackLanguage Then
                    defaultFile = GetResourceFileName(ResourceFileRoot, defaultLanguage)
                    resources = LoadResource(resources, defaultLanguage, cacheKey, defaultFile, CustomizedLocale.None, objPortalSettings)
                    ' Add any host level customizations
                    resources = LoadResource(resources, defaultLanguage, cacheKey, defaultFile, CustomizedLocale.Host, objPortalSettings)
                    ' Add any portal level customizations
                    If Not objPortalSettings Is Nothing Then
                        resources = LoadResource(resources, defaultLanguage, cacheKey, defaultFile, CustomizedLocale.Portal, objPortalSettings)
                    End If
                End If

                ' If the user language is different load it
                If userLanguage <> defaultLanguage AndAlso userLanguage <> fallbackLanguage Then
                    resources = LoadResource(resources, userLanguage, cacheKey, userFile, CustomizedLocale.None, objPortalSettings)
                    ' Add any host level customizations
                    resources = LoadResource(resources, userLanguage, cacheKey, userFile, CustomizedLocale.Host, objPortalSettings)
                    ' Add any portal level customizations
                    If Not objPortalSettings Is Nothing Then
                        resources = LoadResource(resources, userLanguage, cacheKey, userFile, CustomizedLocale.Portal, objPortalSettings)
                    End If
                End If
            End If

            Return resources
        End Function    'GetResource

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetResourceFileName is used to build the resource file name according to the
        ''' language
        ''' </summary>
        ''' <param name="language">The language</param>
        ''' <param name="ResourceFileRoot">The resource file root</param>
        ''' <returns>The language specific resource file</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetResourceFileName(ByVal ResourceFileRoot As String, ByVal language As String) As String

            Dim ResourceFile As String

            If Not ResourceFileRoot Is Nothing Then
                If language = SystemLocale.ToLower Or language = "" Then
                    Select Case Right(ResourceFileRoot, 5).ToLower
                        Case ".resx"
                            ResourceFile = ResourceFileRoot
                        Case ".ascx"
                            ResourceFile = ResourceFileRoot & ".resx"
                        Case ".aspx"
                            ResourceFile = ResourceFileRoot & ".resx"
                        Case Else
                            ResourceFile = ResourceFileRoot + ".ascx.resx"       ' a portal module
                    End Select
                Else
                    Select Case Right(ResourceFileRoot, 5).ToLower
                        Case ".resx"
                            ResourceFile = ResourceFileRoot.Replace(".resx", "." + language + ".resx")
                        Case ".ascx"
                            ResourceFile = ResourceFileRoot.Replace(".ascx", ".ascx." + language + ".resx")
                        Case ".aspx"
                            ResourceFile = ResourceFileRoot.Replace(".aspx", ".aspx." + language + ".resx")
                        Case Else
                            ResourceFile = ResourceFileRoot + ".ascx." + language + ".resx"       ' a portal module
                    End Select
                End If
            Else
                If language = SystemLocale.ToLower Or language = "" Then
                    ResourceFile = SharedResourceFile
                Else
                    ResourceFile = SharedResourceFile.Replace(".resx", "." + language + ".resx")
                End If
            End If

            Return ResourceFile

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' LoadResource is used by getResource to add resources to the resources Hashtable
        ''' </summary>
        ''' <param name="target">The Hashtable to add resources to</param>
        ''' <param name="language">The language to load</param>
        ''' <param name="cacheKey">The cache key</param>
        ''' <param name="ResourceFile">The language specific resource file</param>
        ''' <param name="CheckCustomCulture">A flag to determine if custom resources are to be checked</param>
        ''' <param name="objPortalSettings">The current portals Portal Settings</param>
        ''' <returns>The updated resources Hashtable </returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function LoadResource(ByVal target As Hashtable, ByVal language As String, ByVal cacheKey As String, ByVal ResourceFile As String, ByVal CheckCustomCulture As CustomizedLocale, ByVal objPortalSettings As PortalSettings) As Hashtable

            Dim filePath As String
            Dim f As String = Null.NullString

            'Are we looking for customised resources
            Select Case CheckCustomCulture
                Case CustomizedLocale.None
                    f = ResourceFile
                Case CustomizedLocale.Portal
                    f = ResourceFile.Replace(".resx", ".Portal-" + objPortalSettings.PortalId.ToString + ".resx")
                Case CustomizedLocale.Host
                    f = ResourceFile.Replace(".resx", ".Host.resx")
            End Select

            'If the filename is empty or the file does not exist return the Hashtable
            If f Is Nothing OrElse File.Exists(HttpContext.Current.Server.MapPath(f)) = False Then
                Return target
            End If

            filePath = HttpContext.Current.Server.MapPath(f)

            Dim dp As New CacheDependency(filePath)
            Dim xmlLoaded As Boolean = False
            Dim doc As XPathDocument = Nothing
            Try
                doc = New XPathDocument(filePath)
                xmlLoaded = True
            Catch    'exc As Exception
                xmlLoaded = False
            End Try
            If xmlLoaded Then
                For Each nav As XPathNavigator In doc.CreateNavigator.Select("root/data")
                    If nav.NodeType <> XmlNodeType.Comment Then
                        target(nav.GetAttribute("name", String.Empty)) = nav.SelectSingleNode("value").Value
                    End If
                Next

                Try
                    Dim CacheMins As Integer
                    CacheMins = 3 * Convert.ToInt32(Common.Globals.PerformanceSetting)

                    If CacheMins > 0 Then
                        DataCache.SetCache(cacheKey, target, dp, Date.MaxValue, New TimeSpan(0, CacheMins, 0))
                    End If
                Catch exc As Exception
                End Try
            End If
            Return target
        End Function    'LoadResource 

        Private Shared Function LocalizeTabStripDetails(ByVal arrTabStripDetails As ArrayList) As ArrayList
            Dim i As Integer
            For i = 0 To arrTabStripDetails.Count - 1
                Dim objTab As TabInfo = CType(arrTabStripDetails(i), TabInfo)
                If objTab.IsAdminTab Then
                    Dim strLocalizedTabName As String = Services.Localization.Localization.GetString(objTab.TabName + ".String", Services.Localization.Localization.GlobalResourceFile)
                    If strLocalizedTabName <> "" Then
                        objTab.TabName = strLocalizedTabName
                        objTab.Title = ""
                    End If
                End If
            Next
            Return arrTabStripDetails
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Returns the TimeZone file name for a given resource and language
        ''' </summary>
        ''' <param name="filename">Resource File</param>
        ''' <param name="language">Language</param>
        ''' <returns>Localized File Name</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[vmasanas]	04/10/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function TimeZoneFile(ByVal filename As String, ByVal language As String) As String
            If language = Services.Localization.Localization.SystemLocale Then
                Return filename
            Else
                Return filename.Substring(0, filename.Length - 4) + "." + language + ".xml"
            End If
        End Function

        ''' <summary>
        ''' Provides localization support for DataControlFields used in DetailsView and GridView controls
        ''' </summary>
        ''' <param name="controlField">The field control to localize</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <remarks>
        ''' The header of the DataControlField is localized.
        ''' It also localizes text for following controls: ButtonField, CheckBoxField, CommandField, HyperLinkField, ImageField
        ''' </remarks>
        Private Shared Sub LocalizeDataControlField(ByVal controlField As DataControlField, ByVal resourceFile As String)

            Dim localizedText As String

            'Localize Header Text
            If Not String.IsNullOrEmpty(controlField.HeaderText) Then
                localizedText = GetString((controlField.HeaderText + ".Header"), resourceFile)
                If Not String.IsNullOrEmpty(localizedText) Then
                    controlField.HeaderText = localizedText
                    controlField.AccessibleHeaderText = controlField.HeaderText
                End If
            End If

            Select Case True
                Case TypeOf controlField Is TemplateField
                    'do nothing

                Case TypeOf controlField Is ButtonField
                    Dim button As ButtonField = DirectCast(controlField, ButtonField)
                    localizedText = GetString(button.Text, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then button.Text = localizedText

                Case TypeOf controlField Is CheckBoxField
                    Dim checkbox As CheckBoxField = DirectCast(controlField, CheckBoxField)
                    localizedText = GetString(checkbox.Text, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then checkbox.Text = localizedText

                Case TypeOf controlField Is CommandField
                    Dim commands As CommandField = DirectCast(controlField, CommandField)

                    localizedText = GetString(commands.CancelText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.CancelText = localizedText

                    localizedText = GetString(commands.DeleteText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.DeleteText = localizedText

                    localizedText = GetString(commands.EditText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.EditText = localizedText

                    localizedText = GetString(commands.InsertText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.InsertText = localizedText

                    localizedText = GetString(commands.NewText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.NewText = localizedText

                    localizedText = GetString(commands.SelectText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.SelectText = localizedText

                    localizedText = GetString(commands.UpdateText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then commands.UpdateText = localizedText

                Case TypeOf controlField Is HyperLinkField
                    Dim link As HyperLinkField = DirectCast(controlField, HyperLinkField)
                    localizedText = GetString(link.Text, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then link.Text = localizedText

                Case TypeOf controlField Is ImageField
                    Dim image As ImageField = DirectCast(controlField, ImageField)
                    localizedText = GetString(image.AlternateText, resourceFile)
                    If Not String.IsNullOrEmpty(localizedText) Then image.AlternateText = localizedText

            End Select

        End Sub

#End Region

#Region "Public Methods"

        Public Function AddLocale(ByVal Key As String, ByVal Name As String) As String
            Dim node As XmlNode
            Dim resDoc As New XmlDocument
            Dim a As XmlAttribute
            Dim result As String

            resDoc.Load(HttpContext.Current.Server.MapPath(Localization.SupportedLocalesFile))
            node = resDoc.SelectSingleNode("//root/language[@key='" + Key + "']")
            If Not node Is Nothing Then
                result = "Duplicate.ErrorMessage"
            Else
                node = resDoc.CreateElement("language")
                a = resDoc.CreateAttribute("name")
                a.Value = Name
                node.Attributes.Append(a)
                a = resDoc.CreateAttribute("key")
                a.Value = Key
                node.Attributes.Append(a)
                a = resDoc.CreateAttribute("fallback")
                a.Value = Services.Localization.Localization.SystemLocale
                node.Attributes.Append(a)
                resDoc.SelectSingleNode("//root").AppendChild(node)

                Try
                    resDoc.Save(HttpContext.Current.Server.MapPath(Localization.SupportedLocalesFile))
                    If Not File.Exists(HttpContext.Current.Server.MapPath(TimeZoneFile(Localization.TimezonesFile, Key))) Then
                        File.Copy(HttpContext.Current.Server.MapPath(Localization.TimezonesFile), HttpContext.Current.Server.MapPath(TimeZoneFile(Localization.TimezonesFile, Key)))
                        result = "NewLocale.ErrorMessage"
                    End If
                    result = ""
                Catch
                    result = "Save.ErrorMessage"
                End Try
            End If
            Return result

        End Function

        Public Function GetFixedCurrency(ByVal Expression As Decimal, ByVal Culture As String, Optional ByVal NumDigitsAfterDecimal As Integer = -1, Optional ByVal IncludeLeadingDigit As Microsoft.VisualBasic.TriState = TriState.UseDefault, Optional ByVal UseParensForNegativeNumbers As Microsoft.VisualBasic.TriState = TriState.UseDefault, Optional ByVal GroupDigits As Microsoft.VisualBasic.TriState = TriState.UseDefault) As String
            Dim oldCurrentCulture As String = CurrentCulture
            Dim newCulture As System.Globalization.CultureInfo = New System.Globalization.CultureInfo(Culture)
            System.Threading.Thread.CurrentThread.CurrentUICulture = newCulture
            System.Threading.Thread.CurrentThread.CurrentCulture = newCulture
            Dim currencyStr As String = FormatCurrency(Expression, NumDigitsAfterDecimal, IncludeLeadingDigit, UseParensForNegativeNumbers, GroupDigits)
            Dim oldCulture As System.Globalization.CultureInfo = New System.Globalization.CultureInfo(oldCurrentCulture)
            System.Threading.Thread.CurrentThread.CurrentUICulture = oldCulture
            System.Threading.Thread.CurrentThread.CurrentCulture = oldCulture

            Return currencyStr
        End Function

        Public Function GetFixedDate(ByVal Expression As Date, ByVal Culture As String, Optional ByVal NamedFormat As Microsoft.VisualBasic.DateFormat = DateFormat.GeneralDate, Optional ByVal IncludeLeadingDigit As Microsoft.VisualBasic.TriState = TriState.UseDefault, Optional ByVal UseParensForNegativeNumbers As Microsoft.VisualBasic.TriState = TriState.UseDefault, Optional ByVal GroupDigits As Microsoft.VisualBasic.TriState = TriState.UseDefault) As String
            Dim oldCurrentCulture As String = CurrentCulture
            Dim newCulture As System.Globalization.CultureInfo = New System.Globalization.CultureInfo(Culture)
            System.Threading.Thread.CurrentThread.CurrentUICulture = newCulture
            System.Threading.Thread.CurrentThread.CurrentCulture = newCulture
            Dim dateStr As String = FormatDateTime(Expression, NamedFormat)
            Dim oldCulture As System.Globalization.CultureInfo = New System.Globalization.CultureInfo(oldCurrentCulture)
            System.Threading.Thread.CurrentThread.CurrentUICulture = oldCulture
            System.Threading.Thread.CurrentThread.CurrentCulture = oldCulture

            Return dateStr
        End Function

        Public Shared Function GetResourceFile(ByVal Ctrl As Control, ByVal FileName As String) As String
            Return Ctrl.TemplateSourceDirectory + "/" + Services.Localization.Localization.LocalResourceDirectory + "/" + FileName
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <overloads>One of six overloads</overloads>
        ''' <summary>
        ''' GetString gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <returns>The localized Text</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetString(ByVal name As String) As String

            Dim objPortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)

            Return GetString(name, Nothing, objPortalSettings, Nothing, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <overloads>One of six overloads</overloads>
        ''' <summary>
        ''' GetString gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <param name="objPortalSettings">The current portals Portal Settings</param>
        ''' <returns>The localized Text</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetString(ByVal name As String, ByVal objPortalSettings As PortalSettings) As String

            Return GetString(name, Nothing, objPortalSettings, Nothing, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <overloads>One of six overloads</overloads>
        ''' <summary>
        ''' GetString gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <returns>The localized Text</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetString(ByVal name As String, ByVal ResourceFileRoot As String) As String

            Dim objPortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)

            Return GetString(name, ResourceFileRoot, objPortalSettings, Nothing, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetStringUrl gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <returns>The localized Text</returns>
        ''' <remarks>
        ''' This function should be used to retrieve strings to be used on URLs.
        ''' It is the same as <see cref="GetString">GetString(name,ResourceFileRoot</see> method
        ''' but it disables the ShowMissingKey flag, so even it testing scenarios, the correct string
        ''' is returned
        ''' </remarks>
        ''' <history>
        ''' 	[vmasanas]	11/21/2006	Added
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetStringUrl(ByVal name As String, ByVal ResourceFileRoot As String) As String

            Dim objPortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)

            Return GetString(name, ResourceFileRoot, objPortalSettings, Nothing, True)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <overloads>One of six overloads</overloads>
        ''' <summary>
        ''' GetString gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <param name="strLanguage">A specific language to lookup the string</param>
        ''' <returns>The localized Text</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetString(ByVal name As String, ByVal ResourceFileRoot As String, ByVal strlanguage As String) As String

            Dim objPortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)

            Return GetString(name, ResourceFileRoot, objPortalSettings, strlanguage, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <overloads>One of six overloads</overloads>
        ''' <summary>
        ''' GetString gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <param name="objPortalSettings">The current portals Portal Settings</param>
        ''' <param name="strLanguage">A specific language to lookup the string</param>
        ''' <returns>The localized Text</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetString(ByVal name As String, ByVal ResourceFileRoot As String, ByVal objPortalSettings As PortalSettings, ByVal strLanguage As String) As String

            Return GetString(name, ResourceFileRoot, objPortalSettings, strLanguage, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <overloads>One of six overloads</overloads>
        ''' <summary>
        ''' GetString gets the localized string corresponding to the resourcekey
        ''' </summary>
        ''' <param name="name">The resourcekey to find</param>
        ''' <param name="ResourceFileRoot">The Local Resource root</param>
        ''' <param name="objPortalSettings">The current portals Portal Settings</param>
        ''' <param name="strLanguage">A specific language to lookup the string</param>
        ''' <param name="disableShowMissingKeys">Disables the show missing keys flag</param>
        ''' <returns>The localized Text</returns>
        ''' <history>
        ''' 	[cnurse]	10/06/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetString(ByVal name As String, ByVal ResourceFileRoot As String, ByVal objPortalSettings As PortalSettings, ByVal strLanguage As String, ByVal disableShowMissingKeys As Boolean) As String

            If (name Is Nothing) Then
                Throw New ArgumentNullException("name")
            End If

			'Load the Local Resource Files resources
			Dim resources As Hashtable = GetResource(ResourceFileRoot, objPortalSettings, strLanguage)

			'make the default translation property ".Text"
			If name.IndexOf(".") < 1 Then
				name += ".Text"
			End If

			'If the key can't be found try the Local Shared Resource File resources
			If Not ResourceFileRoot Is Nothing AndAlso (resources Is Nothing OrElse resources(name) Is Nothing) Then
				'try to use a module specific shared resource file
				Dim localSharedFile As String = ResourceFileRoot.Substring(0, ResourceFileRoot.LastIndexOf("/") + 1) & Localization.LocalSharedResourceFile
				resources = GetResource(localSharedFile, objPortalSettings)
			End If

			'If the key can't be found try the Shared Resource Files resources
			If resources Is Nothing OrElse resources(name) Is Nothing Then
				resources = GetResource(Localization.SharedResourceFile, objPortalSettings)
			End If

			'If the key still can't be found then it doesn't exist in the Localization Resources
			If ShowMissingKeys And Not disableShowMissingKeys Then
				If resources Is Nothing OrElse resources(name) Is Nothing Then
					Return "RESX:" & name
				Else
					Return "[L]" & CStr(resources(name))
				End If
			End If

			Return CStr(resources(name))

		End Function

        Public Overloads Shared Function LocaleIsEnabled(ByVal locale As Locale) As Boolean
            Return LocaleIsEnabled(locale.Code)
        End Function

        Public Overloads Shared Function LocaleIsEnabled(ByRef localeCode As String) As Boolean
            Try
                Dim isEnabled As Boolean = False
                Dim collEnabledLocales As LocaleCollection = GetEnabledLocales()
                If collEnabledLocales.Item(localeCode) Is Nothing Then
                    'if localecode is neutral (en, es,...) try to find a locale that has the same language
                    If localeCode.IndexOf("-") = -1 Then
                        For i As Integer = 0 To collEnabledLocales.Count - 1
                            If CType(collEnabledLocales(i).Key, String).Split("-"c)(0) = localeCode Then
                                'set the requested _localecode to the full locale
                                localeCode = CType(collEnabledLocales(i).Key, String)
                                isEnabled = True
                                Exit For
                            End If
                        Next
                    End If
                Else
                    isEnabled = True
                End If
                Return isEnabled
            Catch ex As Exception
                'item could not be retrieved  or error
                Return False
            End Try
        End Function

        Public Shared Function UseBrowserLanguage() As Boolean
            Dim cacheKey As String = ""
            Dim objPortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings()
            Dim usebrowser As Boolean = True

            ' check default host setting
            If String.IsNullOrEmpty(strUseBrowserLanguageDefault) Then
                Dim xmldoc As New XmlDocument
                Dim browserLanguage As XmlNode

                xmldoc.Load(HttpContext.Current.Server.MapPath(Localization.ApplicationResourceDirectory + "/Locales.xml"))
                browserLanguage = xmldoc.SelectSingleNode("//root/browserDetection")
                If Not browserLanguage Is Nothing Then
                    strUseBrowserLanguageDefault = browserLanguage.Attributes("enabled").InnerText
                Else
                    strUseBrowserLanguageDefault = "true"
                End If
            End If
            usebrowser = Boolean.Parse(strUseBrowserLanguageDefault)

            ' check current portal setting
            Dim FilePath As String = HttpContext.Current.Server.MapPath(ApplicationResourceDirectory + "/Locales.Portal-" + objPortalSettings.PortalId.ToString + ".xml")
            If File.Exists(FilePath) Then
                cacheKey = "dotnetnuke-usebrowserlanguage" & objPortalSettings.PortalId.ToString
                Try
                    Dim o As Object = DataCache.GetCache(cacheKey)
                    If o Is Nothing Then
                        Dim xmlLocales As New XmlDocument
                        Dim bXmlLoaded As Boolean = False

                        xmlLocales.Load(FilePath)
                        bXmlLoaded = True

                        Dim d As New XmlDocument
                        d.Load(FilePath)

                        If bXmlLoaded AndAlso Not xmlLocales.SelectSingleNode("//locales/browserDetection") Is Nothing Then
                            usebrowser = Boolean.Parse(xmlLocales.SelectSingleNode("//locales/browserDetection").Attributes("enabled").InnerText)
                        End If
                        If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                            Dim dp As New CacheDependency(FilePath)
                            DataCache.SetCache(cacheKey, usebrowser, dp)
                        End If
                    Else
                        usebrowser = CType(o, Boolean)
                    End If
                Catch ex As Exception
                End Try

                Return usebrowser
            Else
                Return True
            End If

        End Function

        Public Shared Function UseLanguageInUrl() As Boolean
            Dim cacheKey As String = ""
            Dim objPortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings()
            Dim useLanguage As Boolean = True

            ' check default host setting
            If String.IsNullOrEmpty(strUseLanguageInUrlDefault) Then
                Dim xmldoc As New XmlDocument
                Dim languageInUrl As XmlNode

                xmldoc.Load(HttpContext.Current.Server.MapPath(Localization.ApplicationResourceDirectory + "/Locales.xml"))
                languageInUrl = xmldoc.SelectSingleNode("//root/languageInUrl")
                If Not languageInUrl Is Nothing Then
                    strUseLanguageInUrlDefault = languageInUrl.Attributes("enabled").InnerText
                Else
                    strUseLanguageInUrlDefault = "true"
                End If
            End If
            useLanguage = Boolean.Parse(strUseLanguageInUrlDefault)

            ' check current portal setting
            Dim FilePath As String = HttpContext.Current.Server.MapPath(ApplicationResourceDirectory + "/Locales.Portal-" + objPortalSettings.PortalId.ToString + ".xml")
            If File.Exists(FilePath) Then
                cacheKey = "dotnetnuke-uselanguageinurl" & objPortalSettings.PortalId.ToString
                Try
                    Dim o As Object = DataCache.GetCache(cacheKey)
                    If o Is Nothing Then
                        Dim xmlLocales As New XmlDocument
                        Dim bXmlLoaded As Boolean = False

                        xmlLocales.Load(FilePath)
                        bXmlLoaded = True

                        Dim d As New XmlDocument
                        d.Load(FilePath)

                        If bXmlLoaded AndAlso Not xmlLocales.SelectSingleNode("//locales/languageInUrl") Is Nothing Then
                            useLanguage = Boolean.Parse(xmlLocales.SelectSingleNode("//locales/languageInUrl").Attributes("enabled").InnerText)
                        End If
                        If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                            Dim dp As New CacheDependency(FilePath)
                            DataCache.SetCache(cacheKey, useLanguage, dp)
                        End If
                    Else
                        useLanguage = CType(o, Boolean)
                    End If
                Catch ex As Exception
                End Try

                Return useLanguage
            Else
                Return True
            End If

        End Function

        Public Shared Function GetEnabledLocales() As LocaleCollection
            Dim cacheKey As String = ""
            Dim objPortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings()
            Dim FilePath As String = HttpContext.Current.Server.MapPath(ApplicationResourceDirectory + "/Locales.Portal-" + objPortalSettings.PortalId.ToString + ".xml")

            If File.Exists(FilePath) Then
                'locales have been previously disabled
                cacheKey = "dotnetnuke-enabledlocales" & objPortalSettings.PortalId.ToString
                Dim enabledLocales As LocaleCollection = CType(DataCache.GetCache(cacheKey), LocaleCollection)

                If enabledLocales Is Nothing Then
                    enabledLocales = New LocaleCollection
                    Dim xmlInactiveLocales As New XmlDocument
                    Dim bXmlLoaded As Boolean = False

                    'load inactive locales xml file
                    Try
                        xmlInactiveLocales.Load(FilePath)
                        bXmlLoaded = True
                    Catch
                    End Try

                    Dim dp As New CacheDependency(FilePath)
                    Dim d As New XmlDocument
                    d.Load(FilePath)

                    Dim _SupportedLocales As LocaleCollection = GetSupportedLocales()
                    For Each _LocaleCode As String In _SupportedLocales.AllKeys
                        If Not bXmlLoaded OrElse xmlInactiveLocales.SelectSingleNode("//locales/inactive/locale[.='" + _LocaleCode + "']") Is Nothing Then
                            Dim objLocale As New Locale
                            objLocale.Text = _SupportedLocales(_LocaleCode).Text
                            objLocale.Code = _LocaleCode
                            objLocale.Fallback = _SupportedLocales(_LocaleCode).Fallback

                            enabledLocales.Add(_LocaleCode, objLocale)
                        End If
                    Next
                    If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                        DataCache.SetCache(cacheKey, enabledLocales, dp)
                    End If
                End If

                Return enabledLocales
            Else
                ' if portal specific xml file does not exist, all locales have been enabled, so just return supportedlocales
                Return GetSupportedLocales()
            End If
        End Function

        ''' <summary>
        ''' <para>GetSupportedLocales returns the list of locales from the locales.xml file.</para>
        ''' </summary>
        Public Shared Function GetSupportedLocales() As LocaleCollection

            Dim cacheKey As String = "dotnetnuke-supportedlocales"
            Dim supportedLocales As LocaleCollection = CType(DataCache.GetCache(cacheKey), LocaleCollection)

            If supportedLocales Is Nothing Then
                supportedLocales = New LocaleCollection
                Dim filePath As String = Common.Globals.ApplicationMapPath & Localization.SupportedLocalesFile.Substring(1).Replace("/", "\")

                If Not File.Exists(filePath) Then
                    Try
                        ' First access to locales.xml, create using template
                        File.Copy(Common.Globals.ApplicationMapPath & ApplicationResourceDirectory.Substring(1).Replace("/", "\") & "\Locales.xml.config", Common.Globals.ApplicationMapPath & Localization.SupportedLocalesFile.Substring(1).Replace("/", "\"))
                    Catch
                        Dim objLocale As New Locale
                        objLocale.Text = "English"
                        objLocale.Code = "en-US"
                        objLocale.Fallback = ""
                        supportedLocales.Add("en-US", objLocale)
                        Return supportedLocales 'Will be Empty and not cached
                    End Try
                End If

                Dim dp As New CacheDependency(filePath)
                Dim doc As New XPathDocument(filePath)
                For Each nav As XPathNavigator In doc.CreateNavigator.Select("root/language")
                    If nav.NodeType <> XmlNodeType.Comment Then
                        Dim objLocale As New Locale
                        objLocale.Text = nav.GetAttribute("name", "")
                        objLocale.Code = nav.GetAttribute("key", "")
                        objLocale.Fallback = nav.GetAttribute("fallback", "")

                        supportedLocales.Add(nav.GetAttribute("key", ""), objLocale)
                    End If
                Next
                DataCache.SetCache(cacheKey, supportedLocales, dp)
            End If

            Return supportedLocales
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage.
        ''' </summary>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Supported tags:
        ''' - All fields from HostSettings table in the form of: [Host:<b>field</b>]
        ''' - All properties defined in <see cref="T:DotNetNuke.PortalInfo" /> in the form of: [Portal:<b>property</b>]
        ''' - [Portal:URL]: The base URL for the portal
        ''' - All properties defined in <see cref="T:DotNetNuke.UserInfo" /> in the form of: [User:<b>property</b>]
        ''' - All values stored in the user profile in the form of: [Profile:<b>key</b>]
        ''' - [User:VerificationCode]: User verification code for verified registrations
        ''' - [Date:Current]: Current date
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal objPortal As PortalSettings, ByVal MessageName As String) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, Nothing, GlobalResourceFile, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage.
        ''' </summary>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="objUser">Reference to the user used to personalize the message.</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Supported tags:
        ''' - All fields from HostSettings table in the form of: [Host:<b>field</b>]
        ''' - All properties defined in <see cref="T:DotNetNuke.PortalInfo" /> in the form of: [Portal:<b>property</b>]
        ''' - [Portal:URL]: The base URL for the portal
        ''' - All properties defined in <see cref="T:DotNetNuke.UserInfo" /> in the form of: [User:<b>property</b>]
        ''' - All values stored in the user profile in the form of: [Profile:<b>key</b>]
        ''' - [User:VerificationCode]: User verification code for verified registrations
        ''' - [Date:Current]: Current date
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal objUser As UserInfo) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, objUser, GlobalResourceFile, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        '''         ''' Gets a SystemMessage.
        ''' </summary>
        ''' <param name="strLanguage">A specific language to get the SystemMessage for.</param>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="objUser">Reference to the user used to personalize the message.</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Supported tags:
        ''' - All fields from HostSettings table in the form of: [Host:<b>field</b>]
        ''' - All properties defined in <see cref="T:DotNetNuke.PortalInfo" /> in the form of: [Portal:<b>property</b>]
        ''' - [Portal:URL]: The base URL for the portal
        ''' - All properties defined in <see cref="T:DotNetNuke.UserInfo" /> in the form of: [User:<b>property</b>]
        ''' - All values stored in the user profile in the form of: [Profile:<b>key</b>]
        ''' - [User:VerificationCode]: User verification code for verified registrations
        ''' - [Date:Current]: Current date
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal strLanguage As String, ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal objUser As UserInfo) As String
            Return GetSystemMessage(strLanguage, objPortal, MessageName, objUser, GlobalResourceFile, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage.
        ''' </summary>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Supported tags:
        ''' - All fields from HostSettings table in the form of: [Host:<b>field</b>]
        ''' - All properties defined in <see cref="T:DotNetNuke.PortalInfo" /> in the form of: [Portal:<b>property</b>]
        ''' - [Portal:URL]: The base URL for the portal
        ''' - All properties defined in <see cref="T:DotNetNuke.UserInfo" /> in the form of: [User:<b>property</b>]
        ''' - All values stored in the user profile in the form of: [Profile:<b>key</b>]
        ''' - [User:VerificationCode]: User verification code for verified registrations
        ''' - [Date:Current]: Current date
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal ResourceFile As String) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, Nothing, ResourceFile, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage.
        ''' </summary>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="objUser">Reference to the user used to personalize the message.</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Supported tags:
        ''' - All fields from HostSettings table in the form of: [Host:<b>field</b>]
        ''' - All properties defined in <see cref="T:DotNetNuke.PortalInfo" /> in the form of: [Portal:<b>property</b>]
        ''' - [Portal:URL]: The base URL for the portal
        ''' - All properties defined in <see cref="T:DotNetNuke.UserInfo" /> in the form of: [User:<b>property</b>]
        ''' - All values stored in the user profile in the form of: [Profile:<b>key</b>]
        ''' - [User:VerificationCode]: User verification code for verified registrations
        ''' - [Date:Current]: Current date
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal objUser As UserInfo, ByVal ResourceFile As String) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, objUser, ResourceFile, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage passing extra custom parameters to personalize.
        ''' </summary>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <param name="Custom">An ArrayList with replacements for custom tags.</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Custom tags are of the form <b>[Custom:n]</b>, where <b>n</b> is the zero based index which 
        ''' will be used to find the replacement value in <b>Custom</b> parameter.
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        '''     [cnurse]    10/06/2004  Moved from SystemMessages to Localization
        '''     [DanCaron]  10/27/2004  Simplified Profile replacement, added Membership replacement
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal ResourceFile As String, ByVal Custom As ArrayList) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, Nothing, ResourceFile, Custom)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage passing extra custom parameters to personalize.
        ''' </summary>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="objUser">Reference to the user used to personalize the message.</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <param name="Custom">An ArrayList with replacements for custom tags.</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Custom tags are of the form <b>[Custom:n]</b>, where <b>n</b> is the zero based index which 
        ''' will be used to find the replacement value in <b>Custom</b> parameter.
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        '''     [cnurse]    10/06/2004  Moved from SystemMessages to Localization
        '''     [DanCaron]  10/27/2004  Simplified Profile replacement, added Membership replacement
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal objUser As UserInfo, ByVal ResourceFile As String, ByVal Custom As ArrayList) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, objUser, ResourceFile, Custom)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage passing extra custom parameters to personalize.
        ''' </summary>
        ''' <param name="strLanguage">A specific language to get the SystemMessage for.</param>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="objUser">Reference to the user used to personalize the message.</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <param name="Custom">An ArrayList with replacements for custom tags.</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Custom tags are of the form <b>[Custom:n]</b>, where <b>n</b> is the zero based index which 
        ''' will be used to find the replacement value in <b>Custom</b> parameter.
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        '''     [cnurse]    10/06/2004  Moved from SystemMessages to Localization
        '''     [DanCaron]  10/27/2004  Simplified Profile replacement, added Membership replacement
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal strLanguage As String, ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal objUser As UserInfo, ByVal ResourceFile As String, ByVal Custom As ArrayList) As String
            Return GetSystemMessage(Nothing, objPortal, MessageName, objUser, ResourceFile, Custom, "", -1)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a SystemMessage passing extra custom parameters to personalize.
        ''' </summary>
        ''' <param name="strLanguage">A specific language to get the SystemMessage for.</param>
        ''' <param name="objPortal">The portal settings for the portal to which the message will affect.</param>
        ''' <param name="MessageName">The message tag which identifies the SystemMessage.</param>
        ''' <param name="objUser">Reference to the user used to personalize the message.</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <param name="Custom">An ArrayList with replacements for custom tags.</param>
        ''' <param name="CustomCaption">prefix for custom tags</param>
        ''' <param name="AccessingUserID">UserID of the user accessing the system message</param>
        ''' <returns>The message body with all tags replaced.</returns>
        ''' <remarks>
        ''' Custom tags are of the form <b>[Custom:n]</b>, where <b>n</b> is the zero based index which 
        ''' will be used to find the replacement value in <b>Custom</b> parameter.
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	05/07/2004	Documented
        '''     [cnurse]    10/06/2004  Moved from SystemMessages to Localization
        '''     [DanCaron]  10/27/2004  Simplified Profile replacement, added Membership replacement
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSystemMessage(ByVal strLanguage As String, ByVal objPortal As PortalSettings, ByVal MessageName As String, ByVal objUser As UserInfo, ByVal ResourceFile As String, ByVal Custom As ArrayList, ByVal CustomCaption As String, ByVal AccessingUserID As Integer) As String

            Dim strMessageValue As String
            strMessageValue = GetString(MessageName, ResourceFile, objPortal, strLanguage)

            If strMessageValue <> "" Then
                If CustomCaption = "" Then
                    CustomCaption = "Custom"
                End If
                Dim objTokenReplace As New Services.Tokens.TokenReplace(Scope.SystemMessages, strLanguage, objPortal, objUser)
                If (AccessingUserID <> -1) And (Not objUser Is Nothing) Then
                    If objUser.UserID <> AccessingUserID Then
                        objTokenReplace.AccessingUser = New UserController().GetUser(objPortal.PortalId, AccessingUserID)
                    End If
                End If
                strMessageValue = objTokenReplace.ReplaceEnvironmentTokens(strMessageValue, Custom, CustomCaption)
            End If

            Return strMessageValue

        End Function


        '' -----------------------------------------------------------------------------
        '' <summary>
        '' GetTimeZones gets a collection of Tme Zones in the relevant language
        '' </summary>
        '' <param name="language">Language</param>
        ''	<returns>The TimeZones as a Name/Value Collection</returns>
        '' <history>
        '' 	[cnurse]	10/29/2004	Modified to exit gracefully if no relevant file
        '' </history>
        '' -----------------------------------------------------------------------------
        Public Shared Function GetTimeZones(ByVal language As String) As NameValueCollection
            language = language.ToLower
            Dim cacheKey As String = "dotnetnuke-" + language + "-timezones"

            Dim TranslationFile As String

            If language = Services.Localization.Localization.SystemLocale.ToLower Then
                TranslationFile = Services.Localization.Localization.TimezonesFile
            Else
                TranslationFile = Services.Localization.Localization.TimezonesFile.Replace(".xml", "." + language + ".xml")
            End If

            Dim timeZones As NameValueCollection = CType(DataCache.GetCache(cacheKey), NameValueCollection)

            If timeZones Is Nothing Then
                Dim filePath As String = HttpContext.Current.Server.MapPath(TranslationFile)
                timeZones = New NameValueCollection
                If File.Exists(filePath) = False Then
                    Return timeZones
                End If
                Dim dp As New CacheDependency(filePath)
                Try
                    Dim d As New XmlDocument
                    d.Load(filePath)

                    Dim n As XmlNode
                    For Each n In d.SelectSingleNode("root").ChildNodes
                        If n.NodeType <> XmlNodeType.Comment Then
                            timeZones.Add(n.Attributes("name").Value, n.Attributes("key").Value)
                        End If
                    Next n
                Catch ex As Exception

                End Try
                If Common.Globals.PerformanceSetting <> Common.Globals.PerformanceSettings.NoCaching Then
                    DataCache.SetCache(cacheKey, timeZones, dp)
                End If
            End If

            Return timeZones
        End Function    'GetTimeZones

        ''' <summary>
        ''' <para>LoadCultureDropDownList loads a DropDownList with the list of supported cultures
        ''' based on the languages defined in the supported locales file</para>
        ''' </summary>
        ''' <param name="list">DropDownList to load</param>
        ''' <param name="displayType">Format of the culture to display. Must be one the CultureDropDownTypes values. 
        ''' <see cref="CultureDropDownTypes"/> for list of allowable values</param>
        ''' <param name="selectedValue">Name of the default culture to select</param>
        Public Shared Sub LoadCultureDropDownList(ByVal list As DropDownList, ByVal displayType As CultureDropDownTypes, ByVal selectedValue As String)

            Dim supportedLanguages As LocaleCollection = GetSupportedLocales()
            Dim _cultureListItems() As ListItem = New ListItem(supportedLanguages.Count - 1) {}
            Dim _cultureListItemsType As CultureDropDownTypes = displayType
            Dim objPortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings()
            Dim xmlLocales As New XmlDocument
            Dim bXmlLoaded As Boolean = False
            Dim intAdded As Integer = 0

            If File.Exists(HttpContext.Current.Server.MapPath(ApplicationResourceDirectory + "/Locales.Portal-" + objPortalSettings.PortalId.ToString + ".xml")) Then
                Try
                    xmlLocales.Load(HttpContext.Current.Server.MapPath(ApplicationResourceDirectory + "/Locales.Portal-" + objPortalSettings.PortalId.ToString + ".xml"))
                    bXmlLoaded = True
                Catch
                End Try
            End If

            Dim i As Integer
            For i = 0 To supportedLanguages.Count - 1

                ' Only load active locales
                If Not bXmlLoaded OrElse xmlLocales.SelectSingleNode("//locales/inactive/locale[.='" + CType(supportedLanguages(i).Value, Locale).Code + "']") Is Nothing Then
                    ' Create a CultureInfo class based on culture
                    Dim info As CultureInfo = CultureInfo.CreateSpecificCulture(CType(supportedLanguages(i).Value, Locale).Code)

                    ' Create and initialize a new ListItem
                    Dim item As New ListItem
                    item.Value = CType(supportedLanguages(i).Value, Locale).Code

                    ' Based on the display type desired by the user, select the correct property
                    Select Case displayType
                        Case CultureDropDownTypes.EnglishName
                            item.Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(info.EnglishName)
                        Case CultureDropDownTypes.Lcid
                            item.Text = info.LCID.ToString()
                        Case CultureDropDownTypes.Name
                            item.Text = info.Name
                        Case CultureDropDownTypes.NativeName
                            item.Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(info.NativeName)
                        Case CultureDropDownTypes.TwoLetterIsoCode
                            item.Text = info.TwoLetterISOLanguageName
                        Case CultureDropDownTypes.ThreeLetterIsoCode
                            item.Text = info.ThreeLetterISOLanguageName
                        Case Else
                            item.Text = info.DisplayName
                    End Select
                    _cultureListItems(intAdded) = item
                    intAdded += 1
                End If
            Next i

            ' If the drop down list already has items, clear the list
            If list.Items.Count > 0 Then
                list.Items.Clear()
            End If

            ReDim Preserve _cultureListItems(intAdded - 1)
            ' add the items to the list
            list.Items.AddRange(_cultureListItems)

            ' select the default item
            If Not selectedValue Is Nothing Then
                Dim item As ListItem = list.Items.FindByValue(selectedValue)
                If Not item Is Nothing Then
                    list.SelectedIndex = -1
                    item.Selected = True
                End If
            End If
        End Sub    'LoadCultureDropDownList

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' LoadTimeZoneDropDownList loads a drop down list with the Timezones
        ''' </summary>
        ''' <param name="list">The list to load</param>
        ''' <param name="language">Language</param>
        ''' <param name="selectedValue">The selected Time Zone</param>
        ''' <history>
        ''' 	[cnurse]	10/29/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub LoadTimeZoneDropDownList(ByVal list As DropDownList, ByVal language As String, ByVal selectedValue As String)

            Dim timeZones As NameValueCollection = GetTimeZones(language)
            'If no Timezones defined get the System Locale Time Zones
            If timeZones.Count = 0 Then
                timeZones = GetTimeZones(Services.Localization.Localization.SystemLocale.ToLower)
            End If
            Dim i As Integer
            For i = 0 To timeZones.Keys.Count - 1
                list.Items.Add(New ListItem(timeZones.GetKey(i).ToString(), timeZones.Get(i).ToString()))
            Next i

            ' select the default item
            If Not selectedValue Is Nothing Then
                Dim item As ListItem = list.Items.FindByValue(selectedValue)
                If item Is Nothing Then
                    'Try system default
                    item = list.Items.FindByValue(SystemTimeZoneOffset.ToString)
                End If
                If Not item Is Nothing Then
                    list.SelectedIndex = -1
                    item.Selected = True
                End If
            End If

        End Sub    'LoadTimeZoneDropDownList

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Localizes ModuleControl Titles
        ''' </summary>
        ''' <param name="ControlTitle">Current control title</param>
        ''' <param name="ControlSrc">Control Source</param>
        ''' <param name="Key">Control Key</param>
        ''' <returns>
        ''' Localized control title if found
        ''' </returns>
        ''' <remarks>
        ''' Resource keys are: ControlTitle_[key].Text
        ''' Key MUST be lowercase in the resource file
        ''' </remarks>
        ''' <history>
        ''' 	[vmasanas]	08/11/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function LocalizeControlTitle(ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal Key As String) As String
            Dim reskey As String
            reskey = "ControlTitle_" + Key.ToLower + ".Text"
            Dim ResFile As String = ControlSrc.Substring(0, ControlSrc.LastIndexOf("/") + 1) + LocalResourceDirectory + ControlSrc.Substring(ControlSrc.LastIndexOf("/"), ControlSrc.LastIndexOf(".") - ControlSrc.LastIndexOf("/"))
            Dim localizedvalue As String = Services.Localization.Localization.GetString(reskey, ResFile)
            If Not localizedvalue Is Nothing Then
                Return localizedvalue
            Else
                Return ControlTitle
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' LocalizeDataGrid creates localized Headers for a DataGrid
        ''' </summary>
        ''' <param name="grid">Grid to localize</param>
        ''' <param name="ResourceFile">The root name of the Resource File where the localized
        '''   text can be found</param>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub LocalizeDataGrid(ByRef grid As DataGrid, ByVal ResourceFile As String)

            Dim localizedText As String

            For Each col As DataGridColumn In grid.Columns
                'Localize Header Text
                If Not String.IsNullOrEmpty(col.HeaderText) Then
                    localizedText = GetString(col.HeaderText & ".Header", ResourceFile)
                    If localizedText <> "" Then
                        col.HeaderText = localizedText
                    End If
                End If

                If TypeOf col Is EditCommandColumn Then
                    Dim editCol As EditCommandColumn = DirectCast(col, EditCommandColumn)

                    ' Edit Text - maintained for backward compatibility
                    localizedText = GetString(editCol.EditText & ".EditText", ResourceFile)
                    If localizedText <> "" Then editCol.EditText = localizedText

                    ' Edit Text
                    localizedText = GetString(editCol.EditText, ResourceFile)
                    If localizedText <> "" Then editCol.EditText = localizedText

                    ' Cancel Text
                    localizedText = GetString(editCol.CancelText, ResourceFile)
                    If localizedText <> "" Then editCol.CancelText = localizedText

                    ' Update Text
                    localizedText = GetString(editCol.UpdateText, ResourceFile)
                    If localizedText <> "" Then editCol.UpdateText = localizedText
                ElseIf TypeOf col Is ButtonColumn Then
                    Dim buttonCol As ButtonColumn = DirectCast(col, ButtonColumn)

                    ' Edit Text
                    localizedText = GetString(buttonCol.Text, ResourceFile)
                    If localizedText <> "" Then buttonCol.Text = localizedText
                End If

            Next
        End Sub

        ''' <summary>
        ''' Localizes headers and fields on a GridView control
        ''' </summary>
        ''' <param name="gridView">Grid to localize</param>
        ''' <param name="resourceFile">The root name of the resource file where the localized
        '''  texts can be found</param>
        ''' <remarks></remarks>
        Public Shared Sub LocalizeGridView(ByRef gridView As GridView, ByVal resourceFile As String)

            For Each column As DataControlField In gridView.Columns
                LocalizeDataControlField(column, resourceFile)
            Next

        End Sub

        ''' <summary>
        ''' Localizes headers and fields on a DetailsView control
        ''' </summary>
        ''' <param name="detailsView"></param>
        ''' <param name="resourceFile">The root name of the resource file where the localized
        '''  texts can be found</param>
        ''' <remarks></remarks>
        Public Shared Sub LocalizeDetailsView(ByRef detailsView As DetailsView, ByVal resourceFile As String)

            For Each field As DataControlField In detailsView.Fields
                LocalizeDataControlField(field, resourceFile)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Localizes PortalSettings
        ''' </summary>
        ''' <remarks>
        ''' Localizes:
        ''' -DesktopTabs
        ''' -BreadCrumbs
        ''' Localized values are stored in httpcontext
        ''' </remarks>
        ''' <history>
        ''' 	[vmasanas]	11/08/2004	Created
        '''     [vmasanas]  12/09/2004  Localize ActiveTab
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub LocalizePortalSettings()
            Dim objPortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)

            objPortalSettings.DesktopTabs = LocalizeTabStripDetails(objPortalSettings.DesktopTabs)
            objPortalSettings.ActiveTab.BreadCrumbs = LocalizeTabStripDetails(objPortalSettings.ActiveTab.BreadCrumbs)
            If objPortalSettings.ActiveTab.IsAdminTab Then
                Dim strLocalizedTabName As String = Services.Localization.Localization.GetString(objPortalSettings.ActiveTab.TabName + ".String", Services.Localization.Localization.GlobalResourceFile)
                If strLocalizedTabName <> "" Then
                    objPortalSettings.ActiveTab.TabName = strLocalizedTabName
                    objPortalSettings.ActiveTab.Title = ""
                End If
            End If

            HttpContext.Current.Items("PortalSettings") = objPortalSettings
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Localizes the "Built In" Roles
        ''' </summary>
        ''' <remarks>
        ''' Localizes:
        ''' -DesktopTabs
        ''' -BreadCrumbs
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	02/01/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function LocalizeRole(ByVal role As String) As String

            Dim localRole As String

            Select Case role
                Case glbRoleAllUsersName, glbRoleSuperUserName, glbRoleUnauthUserName
                    Dim roleKey As String = role.Replace(" ", "")
                    localRole = GetString(roleKey)
                Case Else
                    localRole = role
            End Select

            Return localRole

        End Function

        Public Shared Sub SetLanguage(ByVal value As String)
            Try
                Dim Response As HttpResponse = HttpContext.Current.Response
                If Response Is Nothing Then
                    Return
                End If

                ' save the pageculture as a cookie
                Dim cookie As System.Web.HttpCookie = Nothing
                cookie = Response.Cookies.Get("language")
                If (cookie Is Nothing) Then
                    If value <> "" Then
                        cookie = New System.Web.HttpCookie("language", value)
                        Response.Cookies.Add(cookie)
                    End If
                Else
                    cookie.Value = value
                    If value <> "" Then
                        Response.Cookies.Set(cookie)
                    Else
                        Response.Cookies.Remove("language")
                    End If
                End If

            Catch
                Return
            End Try
        End Sub

#End Region

    End Class

End Namespace
