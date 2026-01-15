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

Namespace DotNetNuke.Modules.Admin.ResourceInstaller
	Public Class PaFolder
		Private _Name As String
        Private _FolderName As String
        Private _FriendlyName As String
        Private _ModuleName As String
        Private _Description As String
		Private _Version As String
		Private _ResourceFile As String
        Private _ProviderType As String
        Private _BusinessControllerClass As String
        Private _CompatibleVersions As String
        Private _Dependencies As String
        Private _Permissions As String
        Private _SupportsProbingPrivatePath As Boolean
        Private _Modules As ArrayList
		Private _Controls As ArrayList
		Private _Files As ArrayList

        Public Sub New()
            _Name = Null.NullString
            _FolderName = Null.NullString
            _FriendlyName = Null.NullString
            _ModuleName = Null.NullString
            _Description = Null.NullString
            _Version = Null.NullString
            _ResourceFile = Null.NullString
            _ProviderType = Null.NullString
            _BusinessControllerClass = Null.NullString
            _CompatibleVersions = Null.NullString
            _Dependencies = Null.NullString
            _Permissions = Null.NullString
            _SupportsProbingPrivatePath = False
            _Modules = New ArrayList
            _Controls = New ArrayList
            _Files = New ArrayList
        End Sub

        Public Property Name() As String
            Get
                Return _Name
            End Get
            Set(ByVal Value As String)
                _Name = Value
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

        Public Property FriendlyName() As String
            Get
                Return _FriendlyName
            End Get
            Set(ByVal Value As String)
                _FriendlyName = Value
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

        Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
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

        Public Property ResourceFile() As String
            Get
                Return _ResourceFile
            End Get
            Set(ByVal Value As String)
                _ResourceFile = Value
            End Set
        End Property

        Public Property ProviderType() As String
            Get
                Return _ProviderType
            End Get
            Set(ByVal Value As String)
                _ProviderType = Value
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

        Public Property SupportsProbingPrivatePath() As Boolean
            Get
                Return _SupportsProbingPrivatePath
            End Get
            Set(ByVal value As Boolean)
                _SupportsProbingPrivatePath = value
            End Set
        End Property

        Public ReadOnly Property Modules() As ArrayList
            Get
                Return _Modules
            End Get
        End Property

        Public ReadOnly Property Controls() As ArrayList
            Get
                Return _Controls
            End Get
        End Property

        Public ReadOnly Property Files() As ArrayList
            Get
                Return _Files
            End Get
        End Property

    End Class
End Namespace