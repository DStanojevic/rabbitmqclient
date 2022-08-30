Get-ChildItem "$PSScriptRoot\*.ps1" | % { . $_  }

Export-ModuleMember -Function *

$script:global = @{
    SenderUrl          = 'http://localhost:5090/api';
    SubscriberUrl      = 'http://localhost:5003/api'
}