import React, { useEffect, useState } from "react"
import { Link } from "react-router-dom"


export default function Login({ setLoggedIn, loggedIn }) {

    const [user, setuser] = useState([])
    const [signIn, setSign] = useState({
        username: "",
        password: ""
    })

    useEffect(() => {
        fetch("https://uncool-backend.herokuapp.com/users")
            .then(res => res.json())
            .then(res => {
                setuser(res)
            })
    }, [])

    console.log(user)

    const handleChange = event => {
        const {id , value} = event.target
        setSign(prestate => ({
            ...prestate,
            [id] : value
        }))
    }  
    console.log(signIn)
    return(
        <div className="Login" >
            <div className="space" >
            <span><h3>Login Here:</h3></span>
            <form>
                <label>Username:</label>
                <input type="text"
                        id="username"
                        placeholder="Username"
                        // value={signIn.username}
                        // onChange={handleChange}
                />

                <label>Password:</label>
                <input type="text"
                        id="password"
                        placeholder="password"
                        // value={signIn.password}
                        // onChange={handleChange}
                />

                <button
                    type="submit"
                    // onClick=
                >
                    <Link to="/" >Login</Link>
                </button>
                {/* Not a member? <Link to="/register" className="text-warning">
                        Register here
                    </Link> */}
            </form>
        </div>
    </div>
    )
}