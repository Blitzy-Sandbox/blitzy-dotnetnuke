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
Imports System.Configuration
Imports System.Data
Imports System.Globalization
Imports System.Xml.Serialization
Imports DotNetNuke.Services.Tokens

Namespace DotNetNuke.Entities.Modules

	Public Enum VisibilityState
		Maximized
		Minimized
		None
	End Enum

    <XmlRoot("module", IsNullable:=False)> Public Class ModuleInfo
        Implements IPropertyAccess



#Region "Private Members"

        Private _PortalID As Integer
        Private _TabID As Integer
        Private _TabModuleID As Integer
        Private _ModuleID As Integer
        Private _ModuleDefID As Integer
        Private _ModuleOrder As Integer
        Private _PaneName As String
        Private _ModuleTitle As String
        Private _AuthorizedEditRoles As String
        Private _CacheTime As Integer
        Private _AuthorizedViewRoles As String
        Private _Alignment As String
        Private _Color As String
        Private _Border As String
        Private _IconFile As String
        Private _AllTabs As Boolean
        Private _Visibility As VisibilityState
        Private _AuthorizedRoles As String
        Private _IsDeleted As Boolean
        Private _Header As String
        Private _Footer As String
        Private _StartDate As Date
        Private _EndDate As Date
        Private _ContainerSrc As String
        Private _DisplayTitle As Boolean
        Private _DisplayPrint As Boolean
        Private _DisplaySyndicate As Boolean
        Private _InheritViewPermissions As Boolean
        Private _ModulePermissions As Security.Permissions.ModulePermissionCollection
        Private _DesktopModuleID As Integer
        Private _FolderName As String
        Private _FriendlyName As String
        Private _Description As String
        Private _Version As String
        Private _IsPremium As Boolean
        Private _IsAdmin As Boolean
        Private _BusinessControllerClass As String
        Private _ModuleName As String
        Private _SupportedFeatures As Integer
        Private _CompatibleVersions As String
        Private _Dependencies As String
        Private _Permissions As String
        Private _DefaultCacheTime As Integer
        Private _ModuleControlId As Integer
        Private _ControlSrc As String
        Private _ControlType As SecurityAccessLevel
        Private _ControlTitle As String
        Private _HelpUrl As String
        Private _SupportsPartialRendering As Boolean
        Private _ContainerPath As String
        Private _PaneModuleIndex As Integer
        Private _PaneModuleCount As Integer
        Private _IsDefaultModule As Boolean
        Private _AllModules As Boolean

#End Region

#Region "Constructors"

        Public Sub New()
            'initialize the properties that can be null
            'in the database
            _PortalID = Null.NullInteger
            _TabID = Null.NullInteger
            _TabModuleID = Null.NullInteger
            _ModuleID = Null.NullInteger
            _ModuleDefID = Null.NullInteger
            _ModuleTitle = Null.NullString
            _AuthorizedEditRoles = Null.NullString
            _AuthorizedViewRoles = Null.NullString
            _Alignment = Null.NullString
            _Color = Null.NullString
            _Border = Null.NullString
            _IconFile = Null.NullString
            _Header = Null.NullString
            _Footer = Null.NullString
            _StartDate = Null.NullDate
            _EndDate = Null.NullDate
            _ContainerSrc = Null.NullString
            _DisplayTitle = True
            _DisplayPrint = True
            _DisplaySyndicate = False
        End Sub

#End Region

#Region "Public Properties"

        <XmlElement("portalid")> Public Property PortalID() As Integer
            Get
                Return _PortalID
            End Get
            Set(ByVal Value As Integer)
                _PortalID = Value
            End Set
        End Property

        <XmlElement("tabid")> Public Property TabID() As Integer
            Get
                Return _TabID
            End Get
            Set(ByVal Value As Integer)
                _TabID = Value
            End Set
        End Property

        <XmlElement("tabmoduleid")> Public Property TabModuleID() As Integer
            Get
                Return _TabModuleID
            End Get
            Set(ByVal Value As Integer)
                _TabModuleID = Value
            End Set
        End Property

        <XmlElement("moduleID")> Public Property ModuleID() As Integer
            Get
                Return _ModuleID
            End Get
            Set(ByVal Value As Integer)
                _ModuleID = Value
            End Set
        End Property

        <XmlElement("moduledefid")> Public Property ModuleDefID() As Integer
            Get
                Return _ModuleDefID
            End Get
            Set(ByVal Value As Integer)
                _ModuleDefID = Value
            End Set
        End Property

        <XmlElement("moduleorder")> Public Property ModuleOrder() As Integer
            Get
                Return _ModuleOrder
            End Get
            Set(ByVal Value As Integer)
                _ModuleOrder = Value
            End Set
        End Property

        <XmlElement("panename")> Public Property PaneName() As String
            Get
                Return _PaneName
            End Get
            Set(ByVal Value As String)
                _PaneName = Value
            End Set
        End Property

        <XmlElement("title")> Public Property ModuleTitle() As String
            Get
                Return _ModuleTitle
            End Get
            Set(ByVal Value As String)
                _ModuleTitle = Value
            End Set
        End Property

        <XmlElement("cachetime")> Public Property CacheTime() As Integer
            Get
                Return _CacheTime
            End Get
            Set(ByVal Value As Integer)
                _CacheTime = Value
            End Set
        End Property

        <XmlElement("alignment")> Public Property Alignment() As String
            Get
                Return _Alignment
            End Get
            Set(ByVal Value As String)
                _Alignment = Value
            End Set
        End Property

        <XmlElement("color")> Public Property Color() As String
            Get
                Return _Color
            End Get
            Set(ByVal Value As String)
                _Color = Value
            End Set
        End Property

        <XmlElement("border")> Public Property Border() As String
            Get
                Return _Border
            End Get
            Set(ByVal Value As String)
                _Border = Value
            End Set
        End Property

        <XmlElement("iconfile")> Public Property IconFile() As String
            Get
                Return _IconFile
            End Get
            Set(ByVal Value As String)
                _IconFile = Value
            End Set
        End Property

        <XmlElement("alltabs")> Public Property AllTabs() As Boolean
            Get
                Return _AllTabs
            End Get
            Set(ByVal Value As Boolean)
                _AllTabs = Value
            End Set
        End Property

        <XmlElement("visibility")> Public Property Visibility() As VisibilityState
            Get
                Return _Visibility
            End Get
            Set(ByVal Value As VisibilityState)
                _Visibility = Value
            End Set
        End Property

        <XmlElement("isdeleted")> Public Property IsDeleted() As Boolean
            Get
                Return _IsDeleted
            End Get
            Set(ByVal Value As Boolean)
                _IsDeleted = Value
            End Set
        End Property

        <XmlElement("header")> Public Property Header() As String
            Get
                Return _Header
            End Get
            Set(ByVal Value As String)
                _Header = Value
            End Set
        End Property

        <XmlElement("footer")> Public Property Footer() As String
            Get
                Return _Footer
            End Get
            Set(ByVal Value As String)
                _Footer = Value
            End Set
        End Property

        <XmlElement("startdate")> Public Property StartDate() As Date
            Get
                Return _StartDate
            End Get
            Set(ByVal Value As Date)
                _StartDate = Value
            End Set
        End Property

        <XmlElement("enddate")> Public Property EndDate() As Date
            Get
                Return _EndDate
            End Get
            Set(ByVal Value As Date)
                _EndDate = Value
            End Set
        End Property

        <XmlElement("containersrc")> Public Property ContainerSrc() As String
            Get
                Return _ContainerSrc
            End Get
            Set(ByVal Value As String)
                _ContainerSrc = Value
            End Set
        End Property

        <XmlElement("displaytitle")> Public Property DisplayTitle() As Boolean
            Get
                Return _DisplayTitle
            End Get
            Set(ByVal Value As Boolean)
                _DisplayTitle = Value
            End Set
        End Property

        <XmlElement("displayprint")> Public Property DisplayPrint() As Boolean
            Get
                Return _DisplayPrint
            End Get
            Set(ByVal Value As Boolean)
                _DisplayPrint = Value
            End Set
        End Property

        <XmlElement("displaysyndicate")> Public Property DisplaySyndicate() As Boolean
            Get
                Return _DisplaySyndicate
            End Get
            Set(ByVal Value As Boolean)
                _DisplaySyndicate = Value
            End Set
        End Property

        <XmlElement("inheritviewpermissions")> Public Property InheritViewPermissions() As Boolean
            Get
                Return _InheritViewPermissions
            End Get
            Set(ByVal Value As Boolean)
                _InheritViewPermissions = Value
            End Set
        End Property

        <XmlArray("modulepermissions"), XmlArrayItem("permission")> Public Property ModulePermissions() As Security.Permissions.ModulePermissionCollection
            Get
                Return _ModulePermissions
            End Get
            Set(ByVal Value As Security.Permissions.ModulePermissionCollection)
                _ModulePermissions = Value
            End Set
        End Property

        <XmlElement("desktopmoduleid")> Public Property DesktopModuleID() As Integer
            Get
                Return _DesktopModuleID
            End Get
            Set(ByVal Value As Integer)
                _DesktopModuleID = Value
            End Set
        End Property

        <XmlElement("friendlyname")> Public Property FriendlyName() As String
            Get
                Return _FriendlyName
            End Get
            Set(ByVal Value As String)
                _FriendlyName = Value
            End Set
        End Property

        <XmlElement("foldername")> Public Property FolderName() As String
            Get
                Return _FolderName
            End Get
            Set(ByVal Value As String)
                _FolderName = Value
            End Set
        End Property

        <XmlElement("description")> Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
            End Set
        End Property

        <XmlElement("version")> Public Property Version() As String
            Get
                Return _Version
            End Get
            Set(ByVal Value As String)
                _Version = Value
            End Set
        End Property

        <XmlElement("ispremium")> Public Property IsPremium() As Boolean
            Get
                Return _IsPremium
            End Get
            Set(ByVal Value As Boolean)
                _IsPremium = Value
            End Set
        End Property

        <XmlElement("isadmin")> Public Property IsAdmin() As Boolean
            Get
                Return _IsAdmin
            End Get
            Set(ByVal Value As Boolean)
                _IsAdmin = Value
            End Set
        End Property

        <XmlElement("businesscontrollerclass")> Public Property BusinessControllerClass() As String
            Get
                Return _BusinessControllerClass
            End Get
            Set(ByVal Value As String)
                _BusinessControllerClass = Value
            End Set
        End Property

        <XmlElement("modulename")> Public Property ModuleName() As String
            Get
                Return _ModuleName
            End Get
            Set(ByVal Value As String)
                _ModuleName = Value
            End Set
        End Property

        <XmlElement("supportedfeatures")> Public Property SupportedFeatures() As Integer
            Get
                Return (_SupportedFeatures)
            End Get
            Set(ByVal Value As Integer)
                _SupportedFeatures = Value
            End Set
        End Property

        <XmlElement("compatibleversions")> Public Property CompatibleVersions() As String
            Get
                Return _CompatibleVersions
            End Get
            Set(ByVal value As String)
                _CompatibleVersions = value
            End Set
        End Property

        <XmlElement("dependencies")> Public Property Dependencies() As String
            Get
                Return _Dependencies
            End Get
            Set(ByVal value As String)
                _Dependencies = value
            End Set
        End Property

        <XmlElement("permissions")> Public Property Permissions() As String
            Get
                Return _Permissions
            End Get
            Set(ByVal value As String)
                _Permissions = value
            End Set
        End Property

        <XmlElement("defaultcachetime")> Public Property DefaultCacheTime() As Integer
            Get
                Return (_DefaultCacheTime)
            End Get
            Set(ByVal Value As Integer)
                _DefaultCacheTime = Value
            End Set
        End Property

        <XmlElement("modulecontrolid")> Public Property ModuleControlId() As Integer
            Get
                Return _ModuleControlId
            End Get
            Set(ByVal Value As Integer)
                _ModuleControlId = Value
            End Set
        End Property

        <XmlElement("controlsrc")> Public Property ControlSrc() As String
            Get
                Return _ControlSrc
            End Get
            Set(ByVal Value As String)
                _ControlSrc = Value
            End Set
        End Property

        <XmlElement("controltype")> Public Property ControlType() As SecurityAccessLevel
            Get
                Return _ControlType
            End Get
            Set(ByVal Value As SecurityAccessLevel)
                _ControlType = Value
            End Set
        End Property

        <XmlElement("controltitle")> Public Property ControlTitle() As String
            Get
                Return _ControlTitle
            End Get
            Set(ByVal Value As String)
                _ControlTitle = Value
            End Set
        End Property

        <XmlElement("helpurl")> Public Property HelpUrl() As String
            Get
                Return _HelpUrl
            End Get
            Set(ByVal Value As String)
                _HelpUrl = Value
            End Set
        End Property

        <XmlElement("supportspartialrendering")> Public Property SupportsPartialRendering() As Boolean
            Get
                Return _SupportsPartialRendering
            End Get
            Set(ByVal value As Boolean)
                _SupportsPartialRendering = value
            End Set
        End Property

        <XmlElement("authorizededitroles")> Public Property AuthorizedEditRoles() As String
            Get
                Return _AuthorizedEditRoles
            End Get
            Set(ByVal Value As String)
                _AuthorizedEditRoles = Value
            End Set
        End Property

        <XmlElement("authorizedviewroles")> Public Property AuthorizedViewRoles() As String
            Get
                Return _AuthorizedViewRoles
            End Get
            Set(ByVal Value As String)
                _AuthorizedViewRoles = Value
            End Set
        End Property

        <XmlIgnore()> Public Property ContainerPath() As String
            Get
                Return _ContainerPath
            End Get
            Set(ByVal Value As String)
                _ContainerPath = Value
            End Set
        End Property

        <XmlIgnore()> Public Property PaneModuleIndex() As Integer
            Get
                Return _PaneModuleIndex
            End Get
            Set(ByVal Value As Integer)
                _PaneModuleIndex = Value
            End Set
        End Property

        <XmlIgnore()> Public Property PaneModuleCount() As Integer
            Get
                Return _PaneModuleCount
            End Get
            Set(ByVal Value As Integer)
                _PaneModuleCount = Value
            End Set
        End Property

        <XmlIgnore()> Public Property IsDefaultModule() As Boolean
            Get
                Return _IsDefaultModule
            End Get
            Set(ByVal Value As Boolean)
                _IsDefaultModule = Value
            End Set
        End Property

        <XmlIgnore()> Public Property AllModules() As Boolean
            Get
                Return _AllModules
            End Get
            Set(ByVal Value As Boolean)
                _AllModules = Value
            End Set
        End Property

        <XmlIgnore()> Public ReadOnly Property IsPortable() As Boolean
            Get
                Return GetFeature(DesktopModuleSupportedFeature.IsPortable)
            End Get
        End Property

        <XmlIgnore()> Public ReadOnly Property IsSearchable() As Boolean
            Get
                Return GetFeature(DesktopModuleSupportedFeature.IsSearchable)
            End Get
        End Property

        <XmlIgnore()> Public ReadOnly Property IsUpgradeable() As Boolean
            Get
                Return GetFeature(DesktopModuleSupportedFeature.IsUpgradeable)
            End Get
        End Property

        'should be deprecated due to roles being abstracted
        <XmlIgnore()> Public Property AuthorizedRoles() As String
            Get
                Return _AuthorizedRoles
            End Get
            Set(ByVal Value As String)
                _AuthorizedRoles = Value
            End Set
        End Property


#End Region

#Region "Private Helper Methods"

        Private Function GetFeature(ByVal Feature As DesktopModuleSupportedFeature) As Boolean

            Dim isSet As Boolean = False
            'And with the Feature to see if the flag is set
            If (SupportedFeatures And Feature) = Feature Then
                isSet = True
            End If

            Return isSet
        End Function

#End Region

#Region "Public Methods"

        Public Function Clone() As ModuleInfo

            ' create the object
            Dim objModuleInfo As New ModuleInfo

            ' assign the property values
            objModuleInfo.PortalID = Me.PortalID
            objModuleInfo.TabID = Me.TabID
            objModuleInfo.TabModuleID = Me.TabModuleID
            objModuleInfo.ModuleID = Me.ModuleID
            objModuleInfo.ModuleDefID = Me.ModuleDefID
            objModuleInfo.ModuleOrder = Me.ModuleOrder
            objModuleInfo.PaneName = Me.PaneName
            objModuleInfo.ModuleTitle = Me.ModuleTitle
            objModuleInfo.AuthorizedEditRoles = Me.AuthorizedEditRoles
            objModuleInfo.CacheTime = Me.CacheTime
            objModuleInfo.AuthorizedViewRoles = Me.AuthorizedViewRoles
            objModuleInfo.Alignment = Me.Alignment
            objModuleInfo.Color = Me.Color
            objModuleInfo.Border = Me.Border
            objModuleInfo.IconFile = Me.IconFile
            objModuleInfo.AllTabs = Me.AllTabs
            objModuleInfo.Visibility = Me.Visibility
            objModuleInfo.AuthorizedRoles = Me.AuthorizedRoles
            objModuleInfo.IsDeleted = Me.IsDeleted
            objModuleInfo.Header = Me.Header
            objModuleInfo.Footer = Me.Footer
            objModuleInfo.StartDate = Me.StartDate
            objModuleInfo.EndDate = Me.EndDate
            objModuleInfo.ContainerSrc = Me.ContainerSrc
            objModuleInfo.DisplayTitle = Me.DisplayTitle
            objModuleInfo.DisplayPrint = Me.DisplayPrint
            objModuleInfo.DisplaySyndicate = Me.DisplaySyndicate
            objModuleInfo.InheritViewPermissions = Me.InheritViewPermissions
            objModuleInfo.DesktopModuleID = Me.DesktopModuleID
            objModuleInfo.FolderName = Me.FolderName
            objModuleInfo.FriendlyName = Me.FriendlyName
            objModuleInfo.Description = Me.Description
            objModuleInfo.Version = Me.Version
            objModuleInfo.IsAdmin = Me.IsAdmin
            objModuleInfo.IsPremium = Me.IsPremium
            objModuleInfo.BusinessControllerClass = Me.BusinessControllerClass
            objModuleInfo.ModuleName = Me.ModuleName
            objModuleInfo.SupportedFeatures = Me.SupportedFeatures
            objModuleInfo.CompatibleVersions = Me.CompatibleVersions
            objModuleInfo.Dependencies = Me.Dependencies
            objModuleInfo.Permissions = Me.Permissions
            objModuleInfo.DefaultCacheTime = Me.DefaultCacheTime
            objModuleInfo.ModuleControlId = Me.ModuleControlId
            objModuleInfo.ControlSrc = Me.ControlSrc
            objModuleInfo.ControlType = Me.ControlType
            objModuleInfo.ControlTitle = Me.ControlTitle
            objModuleInfo.HelpUrl = Me.HelpUrl
            objModuleInfo.SupportsPartialRendering = Me.SupportsPartialRendering
            objModuleInfo.ContainerPath = Me.ContainerPath
            objModuleInfo.PaneModuleIndex = Me.PaneModuleIndex
            objModuleInfo.PaneModuleCount = Me.PaneModuleCount
            objModuleInfo.IsDefaultModule = Me.IsDefaultModule
            objModuleInfo.AllModules = Me.AllModules

            objModuleInfo.ModulePermissions = Me.ModulePermissions

            Return objModuleInfo

        End Function

        Public Sub Initialize(ByVal PortalId As Integer)
            _PortalID = PortalId
            _TabID = -1
            _ModuleID = -1
            _ModuleDefID = -1
            _ModuleOrder = -1
            _PaneName = ""
            _ModuleTitle = ""
            _AuthorizedEditRoles = ""
            _CacheTime = 0
            _AuthorizedViewRoles = ""
            _Alignment = ""
            _Color = ""
            _Border = ""
            _IconFile = ""
            _AllTabs = False
            _Visibility = VisibilityState.Maximized
            _IsDeleted = False
            _Header = ""
            _Footer = ""
            _StartDate = Null.NullDate
            _EndDate = Null.NullDate
            _DisplayTitle = True
            _DisplayPrint = True
            _DisplaySyndicate = False
            _InheritViewPermissions = False
            _ContainerSrc = ""
            _DesktopModuleID = -1
            _FriendlyName = ""
            _FolderName = ""
            _Description = ""
            _Version = ""
            _IsPremium = False
            _IsAdmin = False
            _BusinessControllerClass = ""
            _ModuleName = ""
            _SupportedFeatures = 0
            _DefaultCacheTime = -1
            _ModuleControlId = -1
            _ControlSrc = ""
            _ControlType = SecurityAccessLevel.Anonymous
            _ControlTitle = ""
            _HelpUrl = ""
            _ContainerPath = ""
            _PaneModuleIndex = 0
            _PaneModuleCount = 0
            _IsDefaultModule = False
            _AllModules = False

            ' get default module settings
            Dim settings As Hashtable = PortalSettings.GetSiteSettings(PortalId)
            If Convert.ToString(settings("defaultmoduleid")) <> "" And Convert.ToString(settings("defaulttabid")) <> "" Then
                Dim objModules As New ModuleController
                Dim objModule As ModuleInfo = objModules.GetModule(Integer.Parse(Convert.ToString(settings("defaultmoduleid"))), Integer.Parse(Convert.ToString(settings("defaulttabid"))), True)
                If Not objModule Is Nothing Then
                    _Alignment = objModule.Alignment
                    _Color = objModule.Color
                    _Border = objModule.Border
                    _IconFile = objModule.IconFile
                    _Visibility = objModule.Visibility
                    _ContainerSrc = objModule.ContainerSrc
                    _DisplayTitle = objModule.DisplayTitle
                    _DisplayPrint = objModule.DisplayPrint
                    _DisplaySyndicate = objModule.DisplaySyndicate
                End If
            End If
        End Sub

#End Region

#Region "IPropertyAccess Implementation"

        Public Function GetProperty(ByVal strPropertyName As String, ByVal strFormat As String, ByVal formatProvider As System.Globalization.CultureInfo, ByVal AccessingUser As Users.UserInfo, ByVal CurrentScope As Scope, ByRef PropertyNotFound As Boolean) As String Implements Services.Tokens.IPropertyAccess.GetProperty

            Dim OutputFormat As String = String.Empty
            If strFormat = String.Empty Then OutputFormat = "g"

            'Content locked for NoSettings
            If CurrentScope = Scope.NoSettings Then PropertyNotFound = True : Return PropertyAccess.ContentLocked

            PropertyNotFound = True
            Dim result As String = String.Empty
            Dim PublicProperty As Boolean = True

            Select Case strPropertyName.ToLower
                Case "portalid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.PortalID.ToString(OutputFormat, formatProvider))
                Case "tabid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.TabID.ToString(OutputFormat, formatProvider))
                Case "tabmoduleid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.TabModuleID.ToString(OutputFormat, formatProvider))
                Case "moduleid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.ModuleID.ToString(OutputFormat, formatProvider))
                Case "moduledefid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.ModuleDefID.ToString(OutputFormat, formatProvider))
                Case "moduleorder"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.ModuleOrder.ToString(OutputFormat, formatProvider))
                Case "panename"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.PaneName, strFormat)
                Case "moduletitle"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ModuleTitle, strFormat)
                Case "cachetime"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.CacheTime.ToString(OutputFormat, formatProvider))
                Case "alignment"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Alignment, strFormat)
                Case "color"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Color, strFormat)
                Case "border"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Border, strFormat)
                Case "iconfile"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.IconFile, strFormat)
                Case "alltabs"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.AllTabs, formatProvider))
                Case "isdeleted"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsDeleted, formatProvider))
                Case "header"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Header, strFormat)
                Case "footer"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Footer, strFormat)
                Case "startdate"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.StartDate.ToString(OutputFormat, formatProvider))
                Case "enddate"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.EndDate.ToString(OutputFormat, formatProvider))
                Case "containersrc"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ContainerSrc, strFormat)
                Case "displaytitle"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.DisplayTitle, formatProvider))
                Case "displayprint"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.DisplayPrint, formatProvider))
                Case "displaysyndicate"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.DisplaySyndicate, formatProvider))
                Case "inheritviewpermissions"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.InheritViewPermissions, formatProvider))
                Case "desktopmoduleid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.DesktopModuleID.ToString(OutputFormat, formatProvider))
                Case "friendlyname"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.FriendlyName, strFormat)
                Case "foldername"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.FolderName, strFormat)
                Case "description"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Description, strFormat)
                Case "version"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Version, strFormat)
                Case "ispremium"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsPremium, formatProvider))
                Case "isadmin"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsAdmin, formatProvider))
                Case "businesscontrollerclass"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.BusinessControllerClass, strFormat)
                Case "modulename"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ModuleName, strFormat)
                Case "supportedfeatures"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.SupportedFeatures.ToString(OutputFormat, formatProvider))
                Case "compatibleversions"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.CompatibleVersions, strFormat)
                Case "dependencies"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Dependencies, strFormat)
                Case "permissions"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Permissions, strFormat)
                Case "defaultcachetime"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.DefaultCacheTime.ToString(OutputFormat, formatProvider))
                Case "modulecontrolid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.ModuleControlId.ToString(OutputFormat, formatProvider))
                Case "controlsrc"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ControlSrc, strFormat)
                Case "controltitle"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ControlTitle, strFormat)
                Case "helpurl"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.HelpUrl, strFormat)
                Case "supportspartialrendering"
                    PublicProperty = True : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.SupportsPartialRendering, formatProvider))
                Case "authorizededitroles"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.AuthorizedEditRoles, strFormat)
                Case "authorizedviewroles"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.AuthorizedViewRoles, strFormat)
                Case "containerpath"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.ContainerPath, strFormat)
                Case "panemoduleindex"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.PaneModuleIndex.ToString(OutputFormat, formatProvider))
                Case "panemodulecount"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.PaneModuleCount.ToString(OutputFormat, formatProvider))
                Case "isdefaultmodule"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsDefaultModule, formatProvider))
                Case "allmodules"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.AllModules, formatProvider))
                Case "isportable"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsPortable, formatProvider))
                Case "issearchable"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsSearchable, formatProvider))
                Case "isupgradeable"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.IsUpgradeable, formatProvider))
                Case "authorizedroles"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.AuthorizedRoles, strFormat)
            End Select

            If Not PublicProperty And CurrentScope <> Scope.Debug Then
                PropertyNotFound = True
                result = PropertyAccess.ContentLocked
            End If

            Return result
        End Function

        Public ReadOnly Property Cacheability() As CacheLevel Implements Services.Tokens.IPropertyAccess.Cacheability
            Get
                Return CacheLevel.fullyCacheable
            End Get
        End Property
#End Region


    End Class


End Namespace
