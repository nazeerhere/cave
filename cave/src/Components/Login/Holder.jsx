import React from "react"
import AccountInfo from "./AccountInfo"
import Log from "./Log"

export default function Holder({ loggedIn }) {
    if(loggedIn) {
        return(
            <div className="AI" >
            <AccountInfo/>
        </div>
        )
    } else {
        return(
            <div className="AI" >
                <Log/>
            </div>
        )
    }
}