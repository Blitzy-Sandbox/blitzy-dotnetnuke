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

Imports System.Collections.Generic
Imports System.IO
Imports System.Xml.XPath

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PackageController class provides the business class for the packages
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/26/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PackageController

#Region "Private Members"

        Private Shared provider As DataProvider = DataProvider.Instance()

#End Region

        Public Shared Function AddPackage(ByVal package As PackageInfo) As Integer
            Dim pckg As PackageInfo = GetPackageByName(package.Name)
            Dim id As Integer = Null.NullInteger
            If pckg Is Nothing Then
                id = provider.AddPackage(package.Name, package.FriendlyName, package.Description, package.PackageType, package.Version, package.License, package.Manifest)
            Else
                package.PackageID = pckg.PackageID
                UpdatePackage(package)
                id = package.PackageID
            End If
            Return id
        End Function

        Public Shared Sub DeletePackage(ByVal packageID As Integer)
            provider.DeletePackage(packageID)
        End Sub

        Public Shared Function GetPackage(ByVal packageID As Integer) As PackageInfo
            Return CBO.FillObject(Of PackageInfo)(provider.GetPackage(packageID))
        End Function

        Public Shared Function GetPackageByName(ByVal name As String) As PackageInfo
            Return CBO.FillObject(Of PackageInfo)(provider.GetPackageByName(name))
        End Function

        Public Shared Function GetPackages() As List(Of PackageInfo)
            Return CBO.FillCollection(Of PackageInfo)(provider.GetPackages())
        End Function

        Public Shared Function GetPackagesByType(ByVal type As String) As List(Of PackageInfo)
            Return CBO.FillCollection(Of PackageInfo)(provider.GetPackagesByType(type))
        End Function

        Public Shared Function GetPackageType(ByVal type As String) As PackageType
            Return CBO.FillObject(Of PackageType)(provider.GetPackageType(type))
        End Function

        Public Shared Sub UpdatePackage(ByVal package As PackageInfo)
            provider.UpdatePackage(package.Name, package.FriendlyName, package.Description, package.PackageType, package.Version, package.License, package.Manifest)
        End Sub

    End Class

End Namespace
