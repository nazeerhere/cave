import React, { useEffect, useState } from "react"
import { Link } from "react-router-dom"

export default function AccountInfo({ setLoggedIn }) {
    const [user, setUser] = useState({
        username: "",
        firstname: "",
        lastname: ""
    })

    function runFunc() {
        setLoggedIn(false)
    }

    console.log("working")
    useEffect(() => {
        fetch("https://uncool-backend.herokuapp.com/users")
            .then(res => res.json())
            .then(res => {
                setUser(res[0])
                console.log(res)
            })
    }, [])

    console.log(user)
    return(
        <div className="AccountInfo" >
                   Welcome 
                <div className="Username" >
                   {user.ussername}
                </div>
            |
            {/* <Link to="/" >
            </Link> */}
                <button id="logout" 
                        onClick={runFunc}
                >
                    Logout
                </button>
        </div>
    )
}