<#
.SYNOPSIS
    Sends GET request to '/messages' endpoint to retrieve all messages that are peristed.

.DESCRIPTION
    After processing messages are persisted. Call this function to obtain all messages.
#>

function Get-AllMessages{
    $params = @{
        Uri         = $script:global.SubscriberUrl
        Method 		= 'GET'
        Endpoint    = "messages"
    }

    Send-Request $params
}