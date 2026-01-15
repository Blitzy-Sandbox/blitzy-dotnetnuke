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
Imports System.Data
Imports DotNetNuke
Imports System.Xml.Serialization

Namespace DotNetNuke.Services.FileSystem

#Region "FolderInfo"
    <XmlRoot("folder", IsNullable:=False)> Public Class FolderInfo

        ' local property declarations
        Private _folderID As Integer
        Private _portalID As Integer
        Private _folderPath As String
        Private _storageLocation As Integer
        Private _isProtected As Boolean
        Private _isCached As Boolean = False
        Private _lastUpdated As Date

        ' initialization
        Public Sub New()
        End Sub    'New

#Region "Public Properties"

        <XmlIgnore()> Public Property FolderID() As Integer
            Get
                Return _folderID
            End Get
            Set(ByVal Value As Integer)
                _folderID = Value
            End Set
        End Property

        <XmlIgnore()> Public ReadOnly Property FolderName() As String
            Get
                Dim _folderName As String = FileSystemUtils.RemoveTrailingSlash(_folderPath)
                If _folderName.Length > 0 AndAlso _folderName.LastIndexOf("/") > -1 Then
                    _folderName = _folderName.Substring(_folderName.LastIndexOf("/") + 1)
                End If
                Return _folderName
            End Get
        End Property

        <XmlElement("folderpath")> Public Property FolderPath() As String
            Get
                Return _folderPath
            End Get
            Set(ByVal Value As String)
                _folderPath = Value
            End Set
        End Property

        <XmlIgnore()> Public Property IsCached() As Boolean
            Get
                Return _isCached
            End Get
            Set(ByVal Value As Boolean)
                _isCached = Value
            End Set
        End Property

        <XmlIgnore()> Public Property IsProtected() As Boolean
            Get
                Return _isProtected
            End Get
            Set(ByVal Value As Boolean)
                _isProtected = Value
            End Set
        End Property

        <XmlIgnore()> Public Property LastUpdated() As Date
            Get
                Return _lastUpdated
            End Get
            Set(ByVal Value As Date)
                _lastUpdated = Value
            End Set
        End Property

        <XmlIgnore()> Public ReadOnly Property PhysicalPath() As String
            Get
                Dim _PhysicalPath As String
                Dim PortalSettings As PortalSettings = Nothing
                If Not HttpContext.Current Is Nothing Then
                    PortalSettings = PortalController.GetCurrentPortalSettings()
                End If

                If PortalId = Null.NullInteger Then
                    _PhysicalPath = DotNetNuke.Common.Globals.HostMapPath + FolderPath
                Else
                    If PortalSettings Is Nothing OrElse PortalSettings.PortalId <> PortalID Then
                        'Get the PortalInfo  based on the Portalid
                        Dim objPortals As New PortalController()
                        Dim objPortal As PortalInfo = objPortals.GetPortal(PortalID)

                        _PhysicalPath = objPortal.HomeDirectoryMapPath + FolderPath
                    Else
                        _PhysicalPath = PortalSettings.HomeDirectoryMapPath + FolderPath
                    End If
                End If

                Return _PhysicalPath.Replace("/", "\")
            End Get
        End Property

        <XmlIgnore()> Public Property PortalID() As Integer
            Get
                Return _portalID
            End Get
            Set(ByVal Value As Integer)
                _portalID = Value
            End Set
        End Property

        <XmlElement("storagelocation")> Public Property StorageLocation() As Integer
            Get
                Return _storageLocation
            End Get
            Set(ByVal Value As Integer)
                _storageLocation = Value
            End Set
        End Property

#End Region

    End Class
#End Region

End Namespace
