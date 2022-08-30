<#
.SYNOPSIS
    Sends POST request with 'SendMessageRequest' payload to Messages controller.

.DESCRIPTION
    This function will send message to rabbit MQ exchange.
#>

function Publish-Message{
    [CmdletBinding()]
    param(
        # Array of users to create; user objects are obtained via New-User
		[Parameter(Mandatory = $true)]
        [PSCustomObject] $SendMessageRequest
    )
    $params = @{
        Uri         = $script:global.SenderUrl
        Method 		= 'POST'
        Endpoint    = "messages"
        Body        = ConvertTo-Json $SendMessageRequest
    }

    
    Send-Request $params
}