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
Imports System.IO
Imports System.Xml

Namespace DotNetNuke.Entities.Modules

    Public Enum DesktopModuleSupportedFeature
        IsPortable = 1
        IsSearchable = 2
        IsUpgradeable = 4
    End Enum

    Public Class DesktopModuleInfo

#Region "Private Members"

        Private _DesktopModuleID As Integer
        Private _ModuleName As String
        Private _FolderName As String
        Private _FriendlyName As String
        Private _Description As String
        Private _Version As String
        Private _IsPremium As Boolean
        Private _IsAdmin As Boolean
        Private _SupportedFeatures As Integer
        Private _BusinessControllerClass As String
        Private _CompatibleVersions As String
        Private _Dependencies As String
        Private _Permissions As String

#End Region

#Region "Contructors"

        Public Sub New()
        End Sub

#End Region

#Region "Properties"

        Public Property DesktopModuleID() As Integer
            Get
                Return _DesktopModuleID
            End Get
            Set(ByVal Value As Integer)
                _DesktopModuleID = Value
            End Set
        End Property

        Public Property ModuleName() As String
            Get
                Return _ModuleName
            End Get
            Set(ByVal Value As String)
                _ModuleName = Value
            End Set
        End Property

        Public Property FriendlyName() As String
            Get
                Return _FriendlyName
            End Get
            Set(ByVal Value As String)
                _FriendlyName = Value
            End Set
        End Property

        Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
            End Set
        End Property

        Public Property FolderName() As String
            Get
                Return _FolderName
            End Get
            Set(ByVal Value As String)
                _FolderName = Value
            End Set
        End Property

        Public Property Version() As String
            Get
                Return _Version
            End Get
            Set(ByVal Value As String)
                _Version = Value
            End Set
        End Property

        Public Property IsPremium() As Boolean
            Get
                Return _IsPremium
            End Get
            Set(ByVal Value As Boolean)
                _IsPremium = Value
            End Set
        End Property

        Public Property IsAdmin() As Boolean
            Get
                Return _IsAdmin
            End Get
            Set(ByVal Value As Boolean)
                _IsAdmin = Value
            End Set
        End Property

        Public Property BusinessControllerClass() As String
            Get
                Return _BusinessControllerClass
            End Get
            Set(ByVal Value As String)
                _BusinessControllerClass = Value
            End Set
        End Property

        Public Property SupportedFeatures() As Integer
            Get
                Return (_SupportedFeatures)
            End Get
            Set(ByVal Value As Integer)
                _SupportedFeatures = Value
            End Set
        End Property

        Public Property IsUpgradeable() As Boolean
            Get
                Return GetFeature(DesktopModuleSupportedFeature.IsUpgradeable)
            End Get
            Set(ByVal Value As Boolean)
                UpdateFeature(DesktopModuleSupportedFeature.IsUpgradeable, Value)
            End Set
        End Property

        Public Property IsPortable() As Boolean
            Get
                Return GetFeature(DesktopModuleSupportedFeature.IsPortable)
            End Get
            Set(ByVal Value As Boolean)
                UpdateFeature(DesktopModuleSupportedFeature.IsPortable, Value)
            End Set
        End Property

        Public Property IsSearchable() As Boolean
            Get
                Return GetFeature(DesktopModuleSupportedFeature.IsSearchable)
            End Get
            Set(ByVal Value As Boolean)
                UpdateFeature(DesktopModuleSupportedFeature.IsSearchable, Value)
            End Set
        End Property

        Public Property CompatibleVersions() As String
            Get
                Return _CompatibleVersions
            End Get
            Set(ByVal Value As String)
                _CompatibleVersions = Value
            End Set
        End Property

        Public Property Dependencies() As String
            Get
                Return _Dependencies
            End Get
            Set(ByVal Value As String)
                _Dependencies = Value
            End Set
        End Property

        Public Property Permissions() As String
            Get
                Return _Permissions
            End Get
            Set(ByVal Value As String)
                _Permissions = Value
            End Set
        End Property

#End Region

#Region "Private Helper Methods"

        Private Sub ClearFeature(ByVal Feature As DesktopModuleSupportedFeature)

            'And with the 1's complement of Feature to Clear the Feature flag
            SupportedFeatures = SupportedFeatures And (Not Feature)

        End Sub

        Private Function GetFeature(ByVal Feature As DesktopModuleSupportedFeature) As Boolean

            Dim isSet As Boolean = False
            'And with the Feature to see if the flag is set
            If SupportedFeatures > Null.NullInteger AndAlso (SupportedFeatures And Feature) = Feature Then
                isSet = True
            End If

            Return isSet
        End Function

        Private Sub SetFeature(ByVal Feature As DesktopModuleSupportedFeature)

            'Or with the Feature to Set the Feature flag
            SupportedFeatures = SupportedFeatures Or Feature

        End Sub

        Private Sub UpdateFeature(ByVal Feature As DesktopModuleSupportedFeature, ByVal IsSet As Boolean)

            If IsSet Then
                SetFeature(Feature)
            Else
                ClearFeature(Feature)
            End If

        End Sub

#End Region

    End Class

End Namespace

