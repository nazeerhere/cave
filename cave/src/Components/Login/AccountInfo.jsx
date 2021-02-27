import React, { useEffect, useState } from "react"
import { Link } from "react-router-dom"

export default function AccountInfo() {
    const [user, setUser] = useState({
        username: "",
        firstname: "",
        lastname: ""
    })

    console.log("working")
    useEffect(() => {
        fetch("http://localhost:8000/users")
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
            <Link to="/" >
                Logout
            </Link>
        </div>
    )
}